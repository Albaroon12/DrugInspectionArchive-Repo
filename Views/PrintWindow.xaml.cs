using DrugInspectionArchive.Services;
using PDFtoImage;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace DrugInspectionArchive.Views;

public sealed record PreviewPage(int Number, BitmapSource Image);

public partial class PrintWindow : Window
{
    private readonly ArchiveStorageService _storage = new();
    private readonly string _relativePath;
    private readonly string _label;

    private readonly ObservableCollection<PreviewPage> _pages = new();

    private byte[] _pdf = [];
    private int _pageCount;

    public PrintWindow(string relativePath, string label)
    {
        InitializeComponent();

        _relativePath = relativePath;
        _label = label;

        LabelText.Text = label;
        PagesList.ItemsSource = _pages;

        PrintButton.IsEnabled = false;

        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            string path = _storage.ResolveFullPath(_relativePath);

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "ملف PDF غير موجود في مجلد الأرشيف");

            _pdf = await File.ReadAllBytesAsync(path);

            await LoadPrintersAsync();

            var progress = new Progress<(int Number, byte[] Png)>(item =>
                _pages.Add(new PreviewPage(item.Number, ToBitmap(item.Png))));

            _pageCount = await Task.Run(() => RenderPreviews(_pdf, progress));

            PagesCountText.Text = _pageCount == 1
                ? "صفحة واحدة"
                : $"{_pageCount} صفحة";

            PrintButton.IsEnabled = QueueBox.Items.Count > 0;
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message, "تعذر تجهيز الكتاب للطباعة", DialogKind.Error);

            Close();
        }
        finally
        {
            LoadBar.Visibility = Visibility.Collapsed;
        }
    }

    private async Task LoadPrintersAsync()
    {
        // نجلب الأسماء فقط في الخيط الخلفي (نصوص آمنة بين الخيوط)
        var (names, defaultName) = await Task.Run(() =>
        {
            var server = new LocalPrintServer();

            var list = server.GetPrintQueues(new[]
                {
                    EnumeratedPrintQueueTypes.Local,
                    EnumeratedPrintQueueTypes.Connections
                })
                .Select(q => q.FullName)
                .OrderBy(n => n)
                .ToList();

            string? name = null;

            try
            {
                name = LocalPrintServer.GetDefaultPrintQueue().FullName;
            }
            catch
            {
            }

            return (list, name);
        });

        QueueBox.ItemsSource = names;

        QueueBox.SelectedItem =
            names.FirstOrDefault(n => n == defaultName) ??
            names.FirstOrDefault();

        if (names.Count == 0)
            StatusText.Text = "لا توجد طابعات مثبتة على هذا الجهاز";
    }

    // إنشاء الطابعة في خيط الواجهة وقت الطباعة
    private static PrintQueue? FindQueue(string name)
    {
        var server = new LocalPrintServer();

        return server.GetPrintQueues(new[]
            {
                EnumeratedPrintQueueTypes.Local,
                EnumeratedPrintQueueTypes.Connections
            })
            .FirstOrDefault(q => q.FullName == name);
    }

    private static int RenderPreviews(
        byte[] pdf, IProgress<(int Number, byte[] Png)> progress)
    {
        using var stream = new MemoryStream(pdf);

        int count = Conversion.GetPageCount(stream, leaveOpen: true);

        var options = new PDFtoImage.RenderOptions { Dpi = 80 };

        for (int i = 0; i < count; i++)
        {
            using var bitmap = Conversion.ToImage(
                stream, i, leaveOpen: true, options: options);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);

            progress.Report((i + 1, data.ToArray()));
        }

        return count;
    }

    private static BitmapSource ToBitmap(byte[] png)
    {
        var image = new BitmapImage();

        using (var stream = new MemoryStream(png))
        {
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
        }

        image.Freeze();

        return image;
    }

    // ===== الإعدادات =====

    private void PagesMode_Changed(object sender, RoutedEventArgs e)
    {
        if (RangeBox is null || RangeChip is null)
            return;

        RangeBox.IsEnabled = RangeChip.IsChecked == true;

        if (RangeBox.IsEnabled)
            RangeBox.Focus();
    }

    private void CopiesMinus_Click(object sender, RoutedEventArgs e) =>
        SetCopies(ReadCopies() - 1);

    private void CopiesPlus_Click(object sender, RoutedEventArgs e) =>
        SetCopies(ReadCopies() + 1);

    private int ReadCopies() =>
        int.TryParse(CopiesBox.Text, out int n) ? n : 1;

    private void SetCopies(int value) =>
        CopiesBox.Text = Math.Clamp(value, 1, 99).ToString();

    // قراءة نطاق مثل 1-3, 5 (يقبل الأرقام العربية)
    private static bool TryParsePages(
        string text, int max, out List<int> pages)
    {
        pages = new List<int>();

        var normalized = new string(text.Select(c => c switch
        {
            >= '\u0660' and <= '\u0669' => (char)('0' + c - '\u0660'),
            >= '\u06F0' and <= '\u06F9' => (char)('0' + c - '\u06F0'),
            '،' or ';' => ',',
            '–' or '—' => '-',
            _ => c
        }).ToArray());

        foreach (string part in normalized.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] ends = part.Split('-', StringSplitOptions.TrimEntries);

            if (ends.Length > 2 || !int.TryParse(ends[0], out int from))
                return false;

            int to = from;

            if (ends.Length == 2 && !int.TryParse(ends[1], out to))
                return false;

            if (from < 1 || to < from || to > max)
                return false;

            for (int p = from; p <= to; p++)
                if (!pages.Contains(p))
                    pages.Add(p);
        }

        return pages.Count > 0;
    }

    // ===== الطباعة =====

    private async void Print_Click(object sender, RoutedEventArgs e)
    {
        PrintQueue? queue = QueueBox.SelectedItem is string selected
    ? FindQueue(selected)
    : null;

        if (queue is null)
        {
            MessageDialog.Show(
                this, "اختر الطابعة أولاً", "الطباعة", DialogKind.Warning);
            return;
        }

        int copies = ReadCopies();

        if (copies < 1 || copies > 99)
        {
            MessageDialog.Show(
                this, "عدد النسخ يجب أن يكون بين 1 و99",
                "الطباعة", DialogKind.Warning);
            return;
        }

        List<int> pages;

        if (RangeChip.IsChecked == true)
        {
            if (!TryParsePages(RangeBox.Text, _pageCount, out pages))
            {
                MessageDialog.Show(
                    this,
                    $"نطاق الصفحات غير صحيح، أدخل أرقاماً بين 1 و{_pageCount} مثل 1-3, 5",
                    "الطباعة", DialogKind.Warning);
                return;
            }
        }
        else
        {
            pages = Enumerable.Range(1, _pageCount).ToList();
        }

        bool color = ColorChip.IsChecked == true;

        IsEnabled = false;
        StatusText.Text = "جارٍ إرسال الكتاب إلى الطابعة";

        await Task.Yield();

        try
        {
            PrintPages(queue, copies, pages, color);

            MessageDialog.Show(
                this,
                $"تم إرسال {_label} إلى الطابعة {queue.FullName}",
                "تمت الطباعة",
                DialogKind.Success);

            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = string.Empty;

            MessageDialog.Show(
                this, ex.Message, "تعذرت الطباعة", DialogKind.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void PrintPages(
        PrintQueue queue, int copies, List<int> pages, bool color)
    {
        var dialog = new PrintDialog { PrintQueue = queue };

        var ticket = dialog.PrintTicket;
        ticket.CopyCount = copies;
        ticket.OutputColor = color ? OutputColor.Color : OutputColor.Monochrome;

        // اتجاه الورقة حسب أول صفحة مختارة
        if (_pages.Count >= pages[0])
        {
            var first = _pages[pages[0] - 1].Image;

            ticket.PageOrientation = first.PixelWidth > first.PixelHeight
                ? PageOrientation.Landscape
                : PageOrientation.Portrait;
        }

        try
        {
            dialog.PrintTicket = queue.MergeAndValidatePrintTicket(
                queue.UserPrintTicket, ticket).ValidatedPrintTicket;
        }
        catch
        {
            dialog.PrintTicket = ticket;
        }

        var paginator = new PdfPaginator(_pdf, pages, color)
        {
            PageSize = new Size(
                dialog.PrintableAreaWidth, dialog.PrintableAreaHeight)
        };

        dialog.PrintDocument(paginator, _label);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}