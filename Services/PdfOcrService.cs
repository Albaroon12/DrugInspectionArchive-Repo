
using System.IO;
using DrugInspectionArchive.Core;
using DrugInspectionArchive.Dtos;
using PDFtoImage;
using SkiaSharp;
using TesseractOCR;
using TesseractOCR.Enums;

namespace DrugInspectionArchive.Services;

public sealed class PdfOcrService : IDisposable
{
    private Engine? _engine;
    private Engine? _productEngine;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _disposed;

    public async Task<OcrDocumentResult> ReadPdfAsync(
        string pdfPath,
        IProgress<OcrPageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            return await Task.Run(
                () => ReadPdfCore(
                    pdfPath,
                    progress,
                    cancellationToken),
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private OcrDocumentResult ReadPdfCore(
        string pdfPath,
        IProgress<OcrPageProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(pdfPath))
            throw new FileNotFoundException(
                "ملف PDF غير موجود.", pdfPath);

        ValidateLanguageFiles();

        _engine ??= new Engine(
            AppPaths.TessDataDirectory,
            "ara+eng",
            EngineMode.Default);

        var pageResults = new List<OcrPageResult>();

        string workDirectory = Path.Combine(
            AppPaths.TempDirectory,
            $"ocr_{Guid.NewGuid():N}");

        Directory.CreateDirectory(workDirectory);

        try
        {
            using var pdfStream = File.OpenRead(pdfPath);

            var renderOptions = new RenderOptions
            {
                Dpi = 300,
                Grayscale = true
            };

            foreach (var bitmap in Conversion.ToImages(
                pdfStream,
                leaveOpen: true,
                password: null,
                options: renderOptions))
            {
                cancellationToken.ThrowIfCancellationRequested();

                int pageNumber = pageResults.Count + 1;

                using (bitmap)
                {
                    int imageWidth = bitmap.Width;
                    int imageHeight = bitmap.Height;

                    string imagePath = Path.Combine(
                        workDirectory,
                        $"page_{pageNumber:D4}.png");

                    try
                    {
                        // تحويل الصورة إلى PNG
                        using var skImage =
                            SKImage.FromBitmap(bitmap);

                        using var encoded =
                            skImage.Encode(
                                SKEncodedImageFormat.Png,
                                100);

                        if (encoded is null)
                        {
                            throw new InvalidOperationException(
                                $"تعذر تحويل الصفحة {pageNumber} إلى صورة.");
                        }

                        using (var output = File.Create(imagePath))
                        {
                            encoded.SaveTo(output);
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        using var pixImage =
                            TesseractOCR.Pix.Image.LoadFromFile(imagePath);

                        using var page = _engine.Process(
                            pixImage,
                            PageSegMode.Auto);

                        // استخراج النص الخام
                        string pageText = page.Text ?? string.Empty;

                        // استخراج بنية الصفحة وإحداثياتها
                        var pageLines = new List<OcrLineResult>();

                        foreach (var block in page.Layout)
                        {
                            foreach (var paragraph in block.Paragraphs)
                            {
                                foreach (var textLine in paragraph.TextLines)
                                {
                                    var lineWords =
                                        new List<OcrWordResult>();

                                    foreach (var word in textLine.Words)
                                    {
                                        var wordBox = word.BoundingBox;

                                        lineWords.Add(new OcrWordResult
                                        {
                                            Text = word.Text ?? string.Empty,

                                            Confidence = word.Confidence,

                                            X1 = wordBox?.X1,
                                            Y1 = wordBox?.Y1,
                                            X2 = wordBox?.X2,
                                            Y2 = wordBox?.Y2
                                        });
                                    }

                                    var lineBox =
                                        textLine.BoundingBox;

                                    pageLines.Add(new OcrLineResult
                                    {
                                        Text = textLine.Text ?? string.Empty,

                                        Confidence = textLine.Confidence,

                                        X1 = lineBox?.X1,
                                        Y1 = lineBox?.Y1,
                                        X2 = lineBox?.X2,
                                        Y2 = lineBox?.Y2,

                                        Words = lineWords
                                    });
                                }
                            }
                        }

                        var allWords = pageLines
                            .SelectMany(x => x.Words)
                            .ToList();

                        var pageResult = new OcrPageResult
                        {
                            PageNumber = pageNumber,

                            Text = pageText,

                            Confidence = NormalizeConfidenceToPercent(
page.MeanConfidence),

                            ImageWidth = imageWidth,

                            ImageHeight = imageHeight,

                            Lines = pageLines,

                            Words = allWords
                        };

                        pageResults.Add(pageResult);

                        // إعادة قراءة عمود المنتجات بالإنجليزية فقط
                        try
                        {
                            var region = TableExtractor.Locate(pageResult);

                            if (region is not null)
                            {
                                pageResult.ProductWords = ReadProductColumn(
                                    bitmap, region, workDirectory, pageNumber);
                            }
                        }
                        catch
                        {
                            // نكمل بدون القراءة الثانية
                        }

                        progress?.Report(
                            new OcrPageProgress(pageNumber));
                    }
                    finally
                    {
                        if (File.Exists(imagePath))
                            File.Delete(imagePath);
                    }
                }
            }

            if (pageResults.Count == 0)
            {
                throw new InvalidOperationException(
                    "لم يتم العثور على صفحات قابلة للقراءة.");
            }

            var fullText = new System.Text.StringBuilder();

            foreach (var result in pageResults)
            {
                fullText.AppendLine(
                    $"========== PAGE {result.PageNumber} ==========");

                fullText.AppendLine(result.Text);
                fullText.AppendLine();
            }

            return new OcrDocumentResult
            {
                FileName = Path.GetFileName(pdfPath),

                PageCount = pageResults.Count,

                AverageConfidence =
                    pageResults.Average(x => x.Confidence),

                FullText = fullText.ToString(),

                Pages = pageResults
            };
        }
        finally
        {
            if (Directory.Exists(workDirectory))
            {
                Directory.Delete(
                    workDirectory,
                    recursive: true);
            }
        }
    }
    private List<OcrWordResult> ReadProductColumn(
    SKBitmap bitmap,
    TableRegion region,
    string workDirectory,
    int pageNumber)
    {
        var words = new List<OcrWordResult>();

        int x1 = Math.Max(0, region.TableLeft);
        int y1 = Math.Max(0, region.Top);
        int x2 = Math.Min(bitmap.Width, region.BatchLeft - 12);
        int y2 = Math.Min(bitmap.Height, region.Bottom);

        if (x2 - x1 < 80 || y2 - y1 < 60)
            return words;

        _productEngine ??= new Engine(
            AppPaths.TessDataDirectory,
            "eng",
            EngineMode.Default);

        string cropPath = Path.Combine(
            workDirectory, $"product_{pageNumber:D4}.png");

        try
        {
            using var crop = new SKBitmap();

            if (!bitmap.ExtractSubset(crop, new SKRectI(x1, y1, x2, y2)))
                return words;

            using var image = SKImage.FromBitmap(crop);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);

            if (encoded is null)
                return words;

            using (var output = File.Create(cropPath))
            {
                encoded.SaveTo(output);
            }

            using var pix = TesseractOCR.Pix.Image.LoadFromFile(cropPath);
            using var page = _productEngine.Process(pix, PageSegMode.Auto);

            foreach (var block in page.Layout)
                foreach (var paragraph in block.Paragraphs)
                    foreach (var line in paragraph.TextLines)
                        foreach (var word in line.Words)
                        {
                            var box = word.BoundingBox;

                            words.Add(new OcrWordResult
                            {
                                Text = word.Text ?? string.Empty,
                                Confidence = word.Confidence,
                                X1 = box?.X1 + x1,
                                Y1 = box?.Y1 + y1,
                                X2 = box?.X2 + x1,
                                Y2 = box?.Y2 + y1
                            });
                        }
        }
        finally
        {
            if (File.Exists(cropPath))
                File.Delete(cropPath);
        }

        return words;
    }

    private static void ValidateLanguageFiles()
    {
        string[] requiredFiles =
        [
            "ara.traineddata",
            "eng.traineddata"
        ];

        foreach (var fileName in requiredFiles)
        {
            string path = Path.Combine(
                AppPaths.TessDataDirectory,
                fileName);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"ملف اللغة غير موجود: {fileName}\n{path}");
            }

            if (new FileInfo(path).Length < 100_000)
            {
                throw new InvalidDataException(
                    $"ملف اللغة {fileName} ناقص أو غير صحيح.");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _engine?.Dispose();
        _engine = null;
        _productEngine?.Dispose();
        _productEngine = null;

        _gate.Dispose();

        _disposed = true;
    }

    private static float NormalizeConfidenceToPercent(
    float confidence)
    {
        if (!float.IsFinite(confidence))
            return 0f;

        // تحويل المقياس العشري إلى نسبة مئوية
        if (confidence >= 0f && confidence <= 1f)
            confidence *= 100f;

        return Math.Clamp(confidence, 0f, 100f);
    }
}
