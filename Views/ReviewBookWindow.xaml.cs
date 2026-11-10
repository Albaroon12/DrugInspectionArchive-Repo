
using DrugInspectionArchive.Dtos;
using DrugInspectionArchive.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using DrugInspectionArchive.Services;


namespace DrugInspectionArchive.Views;

public partial class ReviewBookWindow : Window
{
    private readonly StagedPdfFile _sourceFile;

    private ReviewBookViewModel ViewModel =>
        (ReviewBookViewModel)DataContext;

    public ReviewBookWindow(StagedPdfFile sourceFile)
    {
        InitializeComponent();

        _sourceFile = sourceFile
            ?? throw new ArgumentNullException(nameof(sourceFile));

        DataContext = new ReviewBookViewModel(sourceFile);

        Loaded += ReviewBookWindow_Loaded;
    }

    // تحميل بيانات الكتاب وفتح PDF
    private async void ReviewBookWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            await ViewModel.LoadAsync();

            if (!File.Exists(_sourceFile.TempFilePath))
            {
                ShowPdfError(
                    "النسخة المؤقتة من ملف PDF غير موجودة.");

                return;
            }

            await PdfViewer.EnsureCoreWebView2Async();

            PdfViewer.CoreWebView2.Settings
                .AreDefaultContextMenusEnabled = true;

            PdfViewer.Source = new Uri(
                Path.GetFullPath(_sourceFile.TempFilePath),
                UriKind.Absolute);
        }
        catch (Exception ex)
        {
            ShowPdfError(
                "تعذر فتح ملف PDF.\n" +
                "تأكد من توفر Microsoft Edge WebView2 Runtime.\n\n" +
                ex.Message);
        }
    }

    // عرض أخطاء قارئ PDF
    private void ShowPdfError(string message)
    {
        PdfErrorText.Text = message;

        PdfErrorPanel.Visibility = Visibility.Visible;
    }

    // إضافة شركة جديدة من داخل شاشة المراجعة
    private async void AddCompany_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new AddCompanyWindow(ViewModel.ExtractedCompanyText)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            await ViewModel.ReloadCompaniesAsync(
                dialog.CreatedCompanyName);
        }
    }

    // إضافة سجل مادة جديد
    private void AddMaterial_Click(
        object sender,
        RoutedEventArgs e)
    {
        ViewModel.AddMaterial();

        FocusRowBox(ViewModel.SelectedMaterial, "name");
    }

    // حذف سجل المادة المحدد
    private void RemoveMaterial_Click(
        object sender,
        RoutedEventArgs e)
    {
        ViewModel.RemoveSelectedMaterial();
    }

    // تأكيد المراجعة
    private enum SimilarDecision { Proceed, Cancel, Replaced }

    private async void ConfirmReview_Click(
        object sender,
        RoutedEventArgs e)
    {
        Keyboard.ClearFocus();

        if (!ViewModel.TryConfirmReview(out string validationError))
        {
            MessageDialog.Show(
                this, validationError,
                "بيانات غير مكتملة", DialogKind.Warning);

            return;
        }

        IsEnabled = false;

        try
        {
            // مقارنة داخل الشركة المختارة: رقم الكتاب ثم التاريخ والمواد والباتشات
            var match = await ViewModel.FindSimilarAsync();

            if (match is not null)
            {
                var decision = await ResolveSimilarAsync(match);

                if (decision == SimilarDecision.Cancel)
                    return;

                if (decision == SimilarDecision.Replaced)
                {
                    DialogResult = true;
                    return;
                }
            }

            var (success, error) = await ViewModel.SaveAsync();

            if (!success)
            {
                MessageDialog.Show(
                    this, error, "تعذر الاعتماد", DialogKind.Error);

                return;
            }

            DialogResult = true;
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async Task<SimilarDecision> ResolveSimilarAsync(
        SimilarBookMatch match)
    {
        var existing = match.Existing;

        if (match.IsExact)
        {
            bool replace = MessageDialog.Confirm(
                this,
                "المعلومات التي أدخلتها مطابقة تماماً لكتاب مؤرشف مسبقاً " +
                "(الشركة ورقم الكتاب والتاريخ والمواد وأرقام التشغيلات).\n\n" +
                existing.DetailsText + "\n\n" +
                "هل هذه نسخة جديدة من الكتاب نفسه؟ يمكنك استبدال ملف الـ PDF " +
                "المحفوظ بالملف الجديد، وتبقى جميع البيانات المسجلة كما هي",
                "كتاب مطابق موجود في الأرشيف",
                "استبدال الملف",
                "تراجع",
                kind: DialogKind.Warning);

            if (!replace)
                return SimilarDecision.Cancel;

            var (ok, error) = await ViewModel.ReplaceFileAsync(existing);

            if (!ok)
            {
                MessageDialog.Show(
                    this, error, "تعذر الاستبدال", DialogKind.Error);

                return SimilarDecision.Cancel;
            }

            MessageDialog.Show(
                this,
                "تم استبدال ملف الكتاب بنجاح، وبقيت جميع بياناته المسجلة كما هي.\n\n" +
                existing.DetailsText,
                "تم الاستبدال",
                DialogKind.Success);

            return SimilarDecision.Replaced;
        }

        string lines = string.Join(
            "\n", match.Differences.Take(6).Select(x => "• " + x));

        if (match.Differences.Count > 6)
            lines += $"\nو{match.Differences.Count - 6} فروقات أخرى";

        bool addSeparate = MessageDialog.Confirm(
            this,
            $"يوجد في هذه الشركة كتاب بنفس رقم الكتاب ({existing.BookNumberText}) " +
            "لكن بمعلومات مختلفة:\n\n" + lines + "\n\n" +
            "هل تريد إضافة الكتاب الجديد ككتاب مستقل؟",
            "رقم الكتاب موجود مسبقاً",
            "إضافة ككتاب مستقل",
            "تراجع",
            kind: DialogKind.Warning);

        return addSeparate
            ? SimilarDecision.Proceed
            : SimilarDecision.Cancel;
    }

    // الحصول على ارتباط التاريخ
    private BindingExpression? GetDatePickerBinding()
    {
        var datePicker = FindDatePicker(this);

        return datePicker?.GetBindingExpression(
            DatePicker.SelectedDateProperty);
    }

    // البحث عن DatePicker داخل النافذة
    private static DatePicker? FindDatePicker(
        DependencyObject parent)
    {
        int childCount = System.Windows.Media.VisualTreeHelper
            .GetChildrenCount(parent);

        for (int i = 0; i < childCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper
                .GetChild(parent, i);

            if (child is DatePicker datePicker)
                return datePicker;

            var result = FindDatePicker(child);

            if (result is not null)
                return result;
        }

        return null;
    }

    // إضافة رقم تشغيلة آخر لنفس المادة (زر + في الصف)
    private void RowAddBatch_Click(
        object sender,
        RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext
            is not ReviewMaterialRow row)
            return;

        ViewModel.AddBatchRowAfter(row);

        FocusRowBox(ViewModel.SelectedMaterial, "batch");
    }

    // حذف سجل من الجدول
    private void RowDelete_Click(
        object sender,
        RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext
            is not ReviewMaterialRow row)
            return;

        ViewModel.RemoveRow(row);
    }

    // وضع المؤشر داخل حقل الصف الجديد
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

    private static TextBox? FindTextBox(
        DependencyObject parent,
        string tag)
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

    // إصلاح معروف: التقويم يحتاج نقرتين داخل النافذة المنبثقة
    private void Calendar_GotMouseCapture(
        object sender,
        MouseEventArgs e)
    {
        if (e.OriginalSource is UIElement element &&
            (element is CalendarDayButton || element is CalendarItem))
        {
            element.ReleaseMouseCapture();
        }
    }

    // إغلاق التقويم بعد اختيار التاريخ
    private void Calendar_SelectedDatesChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (FindName("CalendarToggle") is ToggleButton toggle)
            toggle.IsChecked = false;
    }

    // إغلاق شاشة المراجعة
    private void Close_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
