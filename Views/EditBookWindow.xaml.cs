using DrugInspectionArchive.Dtos;
using DrugInspectionArchive.Services;
using DrugInspectionArchive.ViewModels;
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

    // الزر نفسه نجلبه بالاسم وقت التشغيل
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
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        Keyboard.ClearFocus();

        IsEnabled = false;

        try
        {
            var (success, error) = await ViewModel.SaveAsync();

            if (!success)
            {
                MessageDialog.Show(
                    this, error,
                    "تعذر حفظ التعديلات", DialogKind.Error);

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
            $"سيتم حذف كتاب رقم {ViewModel.BookNumber} من الأرشيف.\n" +
            "يبقى ملف الـ PDF محفوظاً، ويمكن استعادة الكتاب عند إضافة الملف نفسه مرة أخرى",
            "حذف كتاب الفحص",
            "حذف الكتاب",
            danger: true,
            kind: DialogKind.Warning);

        if (!confirmed)
            return;

        try
        {
            await ViewModel.DeleteAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message, "تعذر الحذف", DialogKind.Error);
        }
    }

    private void OpenPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _storage.OpenPdf(ViewModel.PdfRelativePath);
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message, "تعذر فتح الملف", DialogKind.Error);
        }
    }

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

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
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