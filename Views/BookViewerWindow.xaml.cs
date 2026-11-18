using DrugInspectionArchive.Services;
using System.IO;
using System.Windows;

namespace DrugInspectionArchive.Views;

public partial class BookViewerWindow : Window
{
    private readonly ArchiveStorageService _storage = new();
    private readonly string _relativePath;
    private readonly string _label;

    public BookViewerWindow(string relativePath, string label)
    {
        InitializeComponent();

        _relativePath = relativePath;
        _label = label;

        LabelText.Text = label;
        Title = label;

        Loaded += async (_, _) => await ShowPdfAsync();
    }

    private async Task ShowPdfAsync()
    {
        try
        {
            string path = _storage.ResolveFullPath(_relativePath);

            if (!File.Exists(path))
            {
                ShowError("ملف PDF غير موجود في مجلد الأرشيف");
                return;
            }

            await PdfViewer.EnsureCoreWebView2Async();

            PdfViewer.Source = new Uri(path, UriKind.Absolute);
        }
        catch (Exception ex)
        {
            ShowError(
                "تعذر فتح ملف PDF\n" +
                "تأكد من توفر Microsoft Edge WebView2 Runtime\n\n" +
                ex.Message);
        }
    }

    private void ShowError(string message)
    {
        PdfErrorText.Text = message;
        PdfErrorPanel.Visibility = Visibility.Visible;
    }

    private void Print_Click(object sender, RoutedEventArgs e) =>
        new PrintWindow(_relativePath, _label) { Owner = this }.ShowDialog();

    private void Share_Click(object sender, RoutedEventArgs e) =>
        new ShareWindow(_relativePath, _label) { Owner = this }.ShowDialog();
}