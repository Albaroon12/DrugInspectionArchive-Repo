using DrugInspectionArchive.Services;
using System.Windows;

namespace DrugInspectionArchive.Views;

public partial class TrashWindow : Window
{
    private readonly TrashService _trash = new();
    private readonly ArchiveStorageService _storage = new();

    private int _count;

    public TrashWindow()
    {
        InitializeComponent();

        Loaded += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        try
        {
            var items = await _trash.GetTrashAsync();

            _count = items.Count;

            TrashGrid.ItemsSource = items;

            CountText.Text = _count switch
            {
                0 => "فارغة",
                1 => "كتاب واحد",
                _ => $"{_count} كتاب"
            };

            EmptyPanel.Visibility =
                _count == 0 ? Visibility.Visible : Visibility.Collapsed;

            EmptyTrashButton.IsEnabled = _count > 0;
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message,
                "تعذر تحميل السلة", DialogKind.Error);
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DeletedBookItem item)
            return;

        try
        {
            _storage.OpenPdf(item.PdfRelativePath);
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message,
                "تعذر فتح الملف", DialogKind.Error);
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DeletedBookItem item)
            return;

        try
        {
            await _trash.RestoreAsync(item.BookId);
            await ReloadAsync();

            MessageDialog.Show(
                this,
                $"تمت استعادة كتاب رقم {item.BookNumberText} ({item.CompanyName}) بنجاح.\n" +
                "يمكنك البحث عنه وتعديل بياناته من الصفحة الرئيسية",
                "تمت الاستعادة",
                DialogKind.Success);
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message,
                "تعذرت الاستعادة", DialogKind.Error);
        }
    }

    private async void Purge_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not DeletedBookItem item)
            return;

        bool confirmed = MessageDialog.Confirm(
            this,
            $"سيتم حذف كتاب رقم {item.BookNumberText} ({item.CompanyName}) " +
            "وجميع بياناته وملف الـ PDF نهائياً من جهازك.\n" +
            "لا يمكن التراجع عن هذه العملية",
            "حذف نهائي",
            "حذف نهائي",
            danger: true,
            kind: DialogKind.Warning);

        if (!confirmed)
            return;

        try
        {
            await _trash.PurgeAsync(item.BookId);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message, "تعذر الحذف", DialogKind.Error);
        }
    }

    private async void EmptyTrash_Click(object sender, RoutedEventArgs e)
    {
        bool confirmed = MessageDialog.Confirm(
            this,
            $"سيتم حذف {_count} كتاب نهائياً مع بياناتها وملفاتها.\n" +
            "لا يمكن التراجع عن هذه العملية",
            "إفراغ سلة المحذوفات",
            "إفراغ السلة",
            danger: true,
            kind: DialogKind.Warning);

        if (!confirmed)
            return;

        try
        {
            await _trash.EmptyTrashAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message, "تعذر الإفراغ", DialogKind.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}