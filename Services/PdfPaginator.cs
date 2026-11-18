using PDFtoImage;
using SkiaSharp;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DrugInspectionArchive.Services;

public sealed class PdfPaginator : DocumentPaginator
{
    private readonly byte[] _pdf;
    private readonly List<int> _pages;
    private readonly bool _color;

    public PdfPaginator(byte[] pdf, List<int> pages, bool color)
    {
        _pdf = pdf;
        _pages = pages;
        _color = color;
    }

    public override bool IsPageCountValid => true;

    public override int PageCount => _pages.Count;

    public override Size PageSize { get; set; }

    public override IDocumentPaginatorSource? Source => null;

    // رقم الصفحة (يبدأ من 1) يُحوَّل لموضع يبدأ من 0
    public override DocumentPage GetPage(int pageNumber)
    {
        using var stream = new MemoryStream(_pdf);

        var options = new PDFtoImage.RenderOptions
        {
            Dpi = 200,
            Grayscale = !_color
        };

        using var bitmap = Conversion.ToImage(
            stream, _pages[pageNumber] - 1, leaveOpen: true, options: options);

        using var skImage = SKImage.FromBitmap(bitmap);
        using var encoded = skImage.Encode(SKEncodedImageFormat.Png, 100);

        var image = new BitmapImage();

        using (var png = new MemoryStream(encoded.ToArray()))
        {
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = png;
            image.EndInit();
        }

        image.Freeze();

        // احتواء الصفحة داخل منطقة الطباعة بنسبتها الأصلية
        double scale = Math.Min(
            PageSize.Width / image.PixelWidth,
            PageSize.Height / image.PixelHeight);

        double width = image.PixelWidth * scale;
        double height = image.PixelHeight * scale;

        var visual = new DrawingVisual();

        System.Windows.Media.RenderOptions.SetBitmapScalingMode(
            visual, BitmapScalingMode.HighQuality);

        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(
                image,
                new Rect(
                    (PageSize.Width - width) / 2,
                    (PageSize.Height - height) / 2,
                    width,
                    height));
        }

        return new DocumentPage(
            visual, PageSize, new Rect(PageSize), new Rect(PageSize));
    }
}