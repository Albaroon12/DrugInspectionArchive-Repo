
using CommunityToolkit.Mvvm.ComponentModel;
using DrugInspectionArchive.Dtos;
using DrugInspectionArchive.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;

namespace DrugInspectionArchive.ViewModels;

public partial class PdfImportViewModel : ObservableObject
{
    private readonly PdfImportService _importService = new();

    private readonly PdfOcrService _ocrService = new();

    private readonly InspectionBookParser _parser = new(); private bool _cleaned;

    public ObservableCollection<StagedPdfFile> PendingFiles
    { get; } = new();

    [ObservableProperty]
    private StagedPdfFile? selectedFile;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage =
        "اختر ملفات PDF أو اسحبها إلى منطقة الإضافة";

    [ObservableProperty]
    private string ocrPreviewText =
        "بعد انتهاء قراءة الملفات، حدد أحدها لعرض النص المستخرج.";

    public int PendingCount => PendingFiles.Count;

    public string PendingCountText =>
        $"عدد الملفات: {PendingCount}";

    public PdfImportViewModel()
    {
        PendingFiles.CollectionChanged +=
            PendingFiles_CollectionChanged;
    }

    private void PendingFiles_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(PendingCountText));
    }

    partial void OnSelectedFileChanged(StagedPdfFile? value)
    {
        OcrPreviewText = value is null
            ? "حدد ملفاً لعرض النص المستخرج."
            : string.IsNullOrWhiteSpace(value.OcrText)
                ? "لم تتم قراءة هذا الملف بعد."
                : value.OcrText;
    }

    // ========================================
    // إضافة ملفات PDF
    // ========================================
    public async Task<List<DuplicateFile>> AddFilesAsync(
        IEnumerable<string> filePaths)
    {
        var duplicates = new List<DuplicateFile>();

        if (IsBusy || _cleaned)
            return duplicates;

        var paths = filePaths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        IsBusy = true;

        int added = 0;
        int queued = 0;
        int failed = 0;

        var errors = new List<string>();

        try
        {
            foreach (var path in paths)
            {
                try
                {
                    var staged = await _importService.StageAsync(path);

                    bool alreadyAdded = PendingFiles.Any(
                        x => string.Equals(
                            x.Sha256,
                            staged.Sha256,
                            StringComparison.OrdinalIgnoreCase));

                    if (alreadyAdded)
                    {
                        await _importService.DiscardAsync(staged);
                        queued++;
                        continue;
                    }

                    PendingFiles.Add(staged);
                    added++;
                }
                catch (DuplicateBookException ex)
                {
                    duplicates.Add(
                        new DuplicateFile(
                            Path.GetFileName(path),
                            ex.Existing));
                }
                catch (Exception ex)
                {
                    failed++;

                    if (errors.Count < 5)
                    {
                        errors.Add(
                            $"{Path.GetFileName(path)}: " +
                            ex.Message.TrimEnd('.', ' '));
                    }
                }
            }

            if (SelectedFile is null && PendingFiles.Count > 0)
                SelectedFile = PendingFiles[0];

            StatusMessage = $"تمت إضافة {added} ملف";

            if (queued > 0)
                StatusMessage += $"، وتجاهل {queued} ملف مكرر داخل القائمة";

            if (duplicates.Count > 0)
                StatusMessage += $"، و{duplicates.Count} ملف موجود في الأرشيف";

            if (failed > 0)
                StatusMessage += $"، وفشل {failed} ملف";

            if (errors.Count > 0)
            {
                StatusMessage +=
                    Environment.NewLine +
                    string.Join(Environment.NewLine, errors);
            }
        }
        finally
        {
            IsBusy = false;
        }

        return duplicates;
    }

    // ========================================
    // بدء قراءة جميع الملفات
    // ========================================

    public async Task StartOcrAsync()
    {
        if (IsBusy || _cleaned)
            return;

        if (PendingFiles.Count == 0)
        {
            StatusMessage = "أضف ملفات PDF أولاً.";
            return;
        }

        var filesToProcess = PendingFiles
            .Where(x => !x.IsOcrCompleted)
            .ToList();

        if (filesToProcess.Count == 0)
        {
            StatusMessage = "جميع الملفات تمت قراءتها مسبقاً";
            return;
        }

        IsBusy = true;

        int succeeded = 0;
        int failed = 0;

        try
        {
            for (int i = 0; i < filesToProcess.Count; i++)
            {
                var file = filesToProcess[i];

                file.ProcessingStatus = "جاري  القراءة";

                StatusMessage =
                    $"الملف {i + 1} من {filesToProcess.Count}: " +
                    file.OriginalFileName;

                try
                {
                    // تقدم قراءة الصفحات
                    var progress =
                        new Progress<OcrPageProgress>(p =>
                        {
                            file.ProcessingStatus =
                                $"جاري قراءة الصفحة {p.PageNumber}";

                            StatusMessage =
                                $"قراءة الملف {i + 1} من {filesToProcess.Count}: " +
                                $"{file.OriginalFileName} - " +
                                $"الصفحة {p.PageNumber}";
                        });

                    var result = await _ocrService.ReadPdfAsync(
                        file.TempFilePath,
                        progress);


                    file.OcrResult = result;

                    file.OcrText = result.FullText;

                    file.PageCount = result.PageCount;

                    file.MeanConfidence = result.AverageConfidence;

                    // تحليل البيانات بعد اكتمال OCR
                    file.ParsedDraft = _parser.Parse(result);

                    // ملف تشخيص لقراءة OCR (للتطوير فقط)
                    OcrDebugWriter.Write(
                        result,
                        file.ParsedDraft,
                        file.OriginalFileName);

                    file.IsOcrCompleted = true;

                    file.ProcessingStatus =
                        $"اكتملت القراءة - {result.PageCount} صفحة - " +
                        $"{file.ParsedDraft.Materials.Count} سجلات محتملة";


                    file.ProcessingStatus =
    $"اكتملت القراءة - {result.PageCount} صفحة، " +
    $"{result.TotalLines} سطر، " +
    $"{result.TotalWords} كلمة";

                    if (SelectedFile == file)
                        OcrPreviewText = result.FullText;

                    succeeded++;
                }
                catch (Exception ex)
                {
                    file.ProcessingStatus = "فشلت القراءة";

                    file.OcrText =
                        $"تعذر قراءة الملف:\n{ex.Message}";

                    if (SelectedFile == file)
                        OcrPreviewText = file.OcrText;

                    failed++;
                }
            }

            StatusMessage =
                $"انتهت قراءة الملفات. " +
                $"نجح: {succeeded}، فشل: {failed}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ========================================
    // إزالة ملف محدد
    // ========================================
    // بعد حفظ الكتاب: نحذف نسخته المؤقتة ونزيله من القائمة
    public async Task RemoveSavedAsync(StagedPdfFile file)
    {
        try
        {
            await _importService.DiscardAsync(file);
        }
        catch
        {
            // الملف المؤقت لا يؤثر على الأرشيف
        }

        PendingFiles.Remove(file);

        if (SelectedFile == file || SelectedFile is null)
            SelectedFile = PendingFiles.FirstOrDefault();

        StatusMessage =
            $"تم حفظ {file.OriginalFileName} في الأرشيف" +
            $"المتبقي في القائمة: {PendingFiles.Count}.";
    }

    public async Task RemoveSelectedAsync()
    {
        if (IsBusy || SelectedFile is null)
        {
            StatusMessage = "حدد ملفاً لإزالته أولاً";
            return;
        }

        IsBusy = true;

        try
        {
            var file = SelectedFile;

            await _importService.DiscardAsync(file);

            PendingFiles.Remove(file);

            SelectedFile = null;

            StatusMessage = "تمت إزالة الملف من القائمة";
        }
        catch (Exception ex)
        {
            StatusMessage =
                $"تعذرت إزالة الملف: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ========================================
    // مسح القائمة
    // ========================================

    public async Task ClearAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;

        try
        {
            foreach (var file in PendingFiles.ToList())
            {
                await _importService.DiscardAsync(file);

                PendingFiles.Remove(file);
            }

            SelectedFile = null;

            OcrPreviewText =
                "بعد انتهاء قراءة الملفات، حدد أحدها لعرض النص المستخرج.";

            StatusMessage = "تم مسح جميع الملفات المؤقتة";
        }
        catch (Exception ex)
        {
            StatusMessage =
                $"حدث خطأ أثناء مسح الملفات: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ========================================
    // التنظيف عند إغلاق النافذة
    // ========================================

    public async Task CleanupAsync()
    {
        if (_cleaned)
            return;

        _cleaned = true;

        foreach (var file in PendingFiles.ToList())
        {
            try
            {
                await _importService.DiscardAsync(file);
            }
            catch
            {
                // نواصل تنظيف الملفات الأخرى
            }
        }

        PendingFiles.Clear();

        SelectedFile = null;

        _ocrService.Dispose();
    }
}
