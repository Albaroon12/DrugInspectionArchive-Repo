using DrugInspectionArchive.Dtos;
using DrugInspectionArchive.Services;
using DrugInspectionArchive.ViewModels;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DrugInspectionArchive.Views;

public partial class EditBookWindow : Window
{
    private readonly ArchiveStorageService _storage = new();

    private EditBookViewModel ViewModel =>
        (EditBookViewModel)DataContext;

    private ToggleButton? CalendarButton =>
        FindName("CalendarToggle") as ToggleButton;

    public EditBookWindow(int bookId)
    {
        InitializeComponent();

        DataContext = new EditBookViewModel(bookId);

        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            await ViewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message,
                "تعذر تحميل الكتاب", DialogKind.Error);

            DialogResult = false;
            return;
        }

        await ShowPdfAsync();
    }

    // ===== عارض الكتاب =====

    private async Task ShowPdfAsync()
    {
        try
        {
            string path = _storage.ResolveFullPath(ViewModel.PdfRelativePath);

            if (!File.Exists(path))
            {
                ShowPdfError("ملف PDF غير موجود في مجلد الأرشيف");
                return;
            }

            PdfErrorPanel.Visibility = Visibility.Collapsed;

            await PdfViewer.EnsureCoreWebView2Async();

            PdfViewer.Source = new Uri(path, UriKind.Absolute);
        }
        catch (Exception ex)
        {
            ShowPdfError(
                "تعذر فتح ملف PDF\n" +
                "تأكد من توفر Microsoft Edge WebView2 Runtime\n\n" +
                ex.Message);
        }
    }

    private void ShowPdfError(string message)
    {
        PdfErrorText.Text = message;
        PdfErrorPanel.Visibility = Visibility.Visible;
    }

    // تحرير الملف من العارض قبل إعادة تسميته أو نقله
    private async Task ReleasePdfAsync()
    {
        try
        {
            if (PdfViewer.CoreWebView2 is not null)
            {
                PdfViewer.CoreWebView2.Navigate("about:blank");
                await Task.Delay(400);
            }
        }
        catch
        {
        }
    }

    // ===== الحفظ والحذف =====

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();

        IsEnabled = false;

        bool released = ViewModel.NumberChanged;

        try
        {
            if (released)
                await ReleasePdfAsync();

            var (success, error) = await ViewModel.SaveAsync();

            if (!success)
            {
                MessageDialog.Show(
                    this, error,
                    "تعذر حفظ التعديلات", DialogKind.Error);

                if (released)
                    await ShowPdfAsync();

                return;
            }

            DialogResult = true;
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        bool confirmed = MessageDialog.Confirm(
            this,
            $"سيتم نقل كتاب رقم {ViewModel.BookNumber} إلى سلة المحذوفات.\n" +
            "يمكنك استعادته منها أو حذفه نهائياً",
            "نقل إلى سلة المحذوفات",
            "نقل إلى السلة",
            danger: true,
            kind: DialogKind.Warning);

        if (!confirmed)
            return;

        await ReleasePdfAsync();

        try
        {
            await ViewModel.DeleteAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message, "تعذر الحذف", DialogKind.Error);

            await ShowPdfAsync();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    // ===== الشركة والمواد =====

    private async void AddCompany_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddCompanyWindow { Owner = this };

        if (dialog.ShowDialog() == true)
            await ViewModel.ReloadCompaniesAsync(dialog.CreatedCompanyName);
    }

    private void AddMaterial_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddMaterial();

        FocusRowBox(ViewModel.SelectedMaterial, "name");
    }

    private void RowAddBatch_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext
            is not ReviewMaterialRow row)
            return;

        ViewModel.AddBatchRowAfter(row);

        FocusRowBox(ViewModel.SelectedMaterial, "batch");
    }

    private void RowDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext
            is not ReviewMaterialRow row)
            return;

        ViewModel.RemoveRow(row);
    }

    private void FocusRowBox(ReviewMaterialRow? row, string tag)
    {
        if (row is null)
            return;

        MaterialsGrid.UpdateLayout();
        MaterialsGrid.ScrollIntoView(row);
        MaterialsGrid.UpdateLayout();

        if (MaterialsGrid.ItemContainerGenerator.ContainerFromItem(row)
            is not DataGridRow container)
            return;

        var box = FindTextBox(container, tag);

        box?.Focus();
        box?.SelectAll();
    }

    private static TextBox? FindTextBox(DependencyObject parent, string tag)
    {
        int count = System.Windows.Media.VisualTreeHelper
            .GetChildrenCount(parent);

        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper
                .GetChild(parent, i);

            if (child is TextBox box && Equals(box.Tag, tag))
                return box;

            var result = FindTextBox(child, tag);

            if (result is not null)
                return result;
        }

        return null;
    }

    // ===== التقويم =====

    private void Calendar_GotMouseCapture(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource is UIElement element &&
            (element is CalendarDayButton || element is CalendarItem))
        {
            element.ReleaseMouseCapture();
        }
    }

    private void Calendar_SelectedDatesChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (CalendarButton is { } toggle)
            toggle.IsChecked = false;
    }
}