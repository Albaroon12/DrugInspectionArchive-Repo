using DrugInspectionArchive.Core;
using DrugInspectionArchive.Dtos;
using DrugInspectionArchive.Enums;
using DrugInspectionArchive.Services;
using DrugInspectionArchive.ViewModels;
using DrugInspectionArchive.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.IO;

namespace DrugInspectionArchive;

public partial class MainWindow : Window
{
    private readonly ArchiveStorageService _storage = new();

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();

        // حجم الاستعادة: 80% من الشاشة وفي المنتصف (ثم تفتح مكبّرة)
        var area = SystemParameters.WorkArea;

        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = area.Width * 0.8;
        Height = area.Height * 0.8;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + (area.Height - Height) / 2;
        WindowState = WindowState.Maximized;

        DataContext = new MainViewModel();

        VersionText.Text = "v" + AppInfo.Version;

        RefreshBranding();

        WarehouseService.Changed += RefreshBranding;
        Closed += (_, _) => WarehouseService.Changed -= RefreshBranding;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await new TrashService().NormalizeArchiveAsync();
        }
        catch
        {
        }

        SyncChips();

        await ViewModel.LoadAsync();

        SearchBox.Focus();
    }

        // عرض اسم المذخر وشعاره في الشريط العلوي
    private void RefreshBranding()
    {
        var logo = WarehouseService.LoadLogo();

        WarehouseNameText.Text = WarehouseService.Current?.Name ?? string.Empty;
        WarehouseInitialText.Text = WarehouseService.InitialOf(WarehouseNameText.Text);

        WarehouseLogoImage.Source = logo;
        WarehouseLogoBadge.Visibility = logo is null ? Visibility.Collapsed : Visibility.Visible;
        WarehouseInitialBadge.Visibility = logo is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SyncChips()
    {
        foreach (var rb in ModeChips.Children.OfType<RadioButton>())
            rb.IsChecked = (string?)rb.Tag == ViewModel.Mode.ToString();

        foreach (var rb in SortChips.Children.OfType<RadioButton>())
            rb.IsChecked = (string?)rb.Tag == ViewModel.Sort.ToString();
    }

    // ===== الشريط العلوي =====

    private async void AddCompany_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddCompanyWindow { Owner = this };

        if (dialog.ShowDialog() == true)
            await ViewModel.LoadAsync();
    }

    private async void AddBooks_Click(object sender, RoutedEventArgs e)
    {
        new AddBooksWindow { Owner = this }.ShowDialog();

        await ViewModel.LoadAsync();

        SearchBox.Focus();
    }

    private async void Trash_Click(object sender, RoutedEventArgs e)
    {
        new TrashWindow { Owner = this }.ShowDialog();

        await ViewModel.LoadAsync();
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        new SettingsWindow { Owner = this }.ShowDialog();

        ViewModel.Mode = AppSettings.DefaultMode;
        ViewModel.Sort = AppSettings.DefaultSort;

        SyncChips();

        await ViewModel.LoadAsync();
    }

    private void About_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    // ===== البحث (متوافق مع قارئ الباركود) =====

    private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;

            await ViewModel.SearchNowAsync();

            SearchBox.SelectAll();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ViewModel.Query = string.Empty;
        }
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Query = string.Empty;
        SearchBox.Focus();
    }

    private void SearchMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } &&
            Enum.TryParse(tag, out SearchMode mode))
        {
            ViewModel.Mode = mode;
        }

        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag } &&
            Enum.TryParse(tag, out BookSort sort))
        {
            ViewModel.Sort = sort;
        }
    }

    private void Window_PreviewTextInput(
        object sender,
        TextCompositionEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBoxBase or ComboBox)
            return;

        if (string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]))
            return;

        e.Handled = true;

        SearchBox.Focus();
        SearchBox.SelectAll();
        SearchBox.SelectedText = e.Text;
        SearchBox.CaretIndex = SearchBox.Text.Length;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
    }

    // ===== إجراءات الكتب =====

    private void BookOpen_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is BookArchiveItem book)
            OpenPdf(book.PdfRelativePath);
    }

    private async void BookEdit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is BookArchiveItem book)
            await EditBookAsync(book.Id);
    }

    private async void BookDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not BookArchiveItem book)
            return;

        bool confirmed = MessageDialog.Confirm(
            this,
            $"سيتم نقل كتاب رقم {book.BookNumberText} ({book.CompanyName}) إلى سلة المحذوفات.\n" +
            "يمكنك استعادته منها أو حذفه نهائياً",
            "نقل إلى سلة المحذوفات",
            "نقل إلى السلة",
            danger: true,
            kind: DialogKind.Warning);

        if (!confirmed)
            return;

        try
        {
            await new TrashService().MoveToTrashAsync(book.Id);
            await ViewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            MessageDialog.Show(this, ex.Message, "تعذر الحذف", DialogKind.Error);
        }
    }

    private void BooksGrid_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (RowFrom(BooksGrid, e) is { Item: BookArchiveItem book })
            OpenPdf(book.PdfRelativePath);
    }

    // ===== إجراءات النتائج =====

    private void ResultOpen_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ArchiveSearchResult r)
            OpenPdf(r.PdfRelativePath);
    }

    private async void ResultEdit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ArchiveSearchResult r)
            await EditBookAsync(r.BookId);
    }

    private void ResultsGrid_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (RowFrom(ResultsGrid, e) is { Item: ArchiveSearchResult r })
            OpenPdf(r.PdfRelativePath);
    }

    // ===== مساعدات =====

    private static DataGridRow? RowFrom(DataGrid grid, MouseButtonEventArgs e) =>
        ItemsControl.ContainerFromElement(
            grid, e.OriginalSource as DependencyObject) as DataGridRow;

    private async Task EditBookAsync(int bookId)
    {
        var window = new EditBookWindow(bookId) { Owner = this };

        if (window.ShowDialog() == true)
            await ViewModel.LoadAsync();
    }

    // اسم الكتاب من اسم الملف: Inspection Drug #1231
    private static string LabelOf(string relativePath) =>
        Path.GetFileNameWithoutExtension(relativePath).Replace("#", " #");

    // عرض الكتاب داخل البرنامج بدل المتصفح
    private void OpenPdf(string relativePath)
    {
        try
        {
            if (!_storage.Exists(relativePath))
                throw new FileNotFoundException(
                    "ملف PDF غير موجود في مجلد الأرشيف");

            new BookViewerWindow(relativePath, LabelOf(relativePath))
            {
                Owner = this
            }.Show();
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
                this, ex.Message, "تعذر فتح الملف", DialogKind.Error);
        }
    }

    private void PrintPdf(string relativePath)
    {
        if (!_storage.Exists(relativePath))
        {
            MessageDialog.Show(
                this, "ملف PDF غير موجود في مجلد الأرشيف",
                "تعذرت الطباعة", DialogKind.Error);
            return;
        }

        new PrintWindow(relativePath, LabelOf(relativePath)) { Owner = this }
            .ShowDialog();
    }

    private void SharePdf(string relativePath)
    {
        if (!_storage.Exists(relativePath))
        {
            MessageDialog.Show(
                this, "ملف PDF غير موجود في مجلد الأرشيف",
                "تعذرت المشاركة", DialogKind.Error);
            return;
        }

        new ShareWindow(relativePath, LabelOf(relativePath)) { Owner = this }
            .ShowDialog();
    }

    private void BookPrint_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is BookArchiveItem book)
            PrintPdf(book.PdfRelativePath);
    }

    private void BookShare_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is BookArchiveItem book)
            SharePdf(book.PdfRelativePath);
    }

    private void ResultPrint_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ArchiveSearchResult r)
            PrintPdf(r.PdfRelativePath);
    }

    private void ResultShare_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ArchiveSearchResult r)
            SharePdf(r.PdfRelativePath);
    }
}