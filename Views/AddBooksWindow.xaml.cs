
using DrugInspectionArchive.ViewModels;
using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DrugInspectionArchive.Services;


namespace DrugInspectionArchive.Views;

public partial class AddBooksWindow : Window
{
    private bool _allowClose;
    private bool _cleanupStarted;

    private PdfImportViewModel ViewModel =>
        (PdfImportViewModel)DataContext;

    public AddBooksWindow()
    {
        InitializeComponent();

        DataContext = new PdfImportViewModel();

        Closing += AddBooksWindow_Closing;
    }

    // اختيار عدة ملفات PDF
    private async void ChooseFiles_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "اختيار كتب فحص الأدوية",
            Filter = "PDF Files (*.pdf)|*.pdf",
            Multiselect = true,
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await AddAndReportAsync(dialog.FileNames);
        }
    }

    // السحب والإفلات
    private void DropZone_DragOver(
        object sender,
        DragEventArgs e)
    {
        e.Effects = !ViewModel.IsBusy &&
                    e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }

    // استقبال الملفات المسحوبة
    private async void DropZone_Drop(
        object sender,
        DragEventArgs e)
    {
        if (ViewModel.IsBusy)
            return;

        if (e.Data.GetData(DataFormats.FileDrop)
            is string[] files)
        {
            await AddAndReportAsync(files);
        }
    }

    // إزالة الملف المحدد
    private async void RemoveSelected_Click(
        object sender,
        RoutedEventArgs e)
    {
        await ViewModel.RemoveSelectedAsync();
    }

    // مسح قائمة الملفات
    private async void ClearFiles_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (ViewModel.PendingCount == 0 ||
            ViewModel.IsBusy)
            return;

        if (MessageDialog.Confirm(
            this,
            "سيتم إزالة جميع الملفات من القائمة. ملفاتك الأصلية لن تتأثر",
            "إفراغ القائمة",
            "إزالة الكل",
            danger: true,
            kind: DialogKind.Warning))
        {
            await ViewModel.ClearAsync();
        }
    }

    // زر الإغلاق داخل النافذة
    private void Close_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    // التحكم بعملية الإغلاق سواء من زر X أو زر الإغلاق
    private void AddBooksWindow_Closing(
        object? sender,
        CancelEventArgs e)
    {
        // السماح بالإغلاق النهائي بعد تنظيف الملفات
        if (_allowClose)
            return;

        // إلغاء الإغلاق الأول
        e.Cancel = true;

        // منع بدء أكثر من عملية تنظيف
        if (_cleanupStarted)
            return;

        // لا نغلق أثناء معالجة الملفات
        if (ViewModel.IsBusy)
        {
            ViewModel.StatusMessage =
                "انتظر انتهاء معالجة الملفات قبل إغلاق النافذة";

            return;
        }

        // تحذير المستخدم إذا كانت هناك ملفات معلقة
        if (ViewModel.PendingCount > 0)
        {
            if (!MessageDialog.Confirm(
    this,
    "توجد ملفات لم تتم مراجعتها بعد.\n" +
    "إغلاق النافذة سيحذف النسخ المؤقتة فقط، " +
    "ولن تتأثر الملفات الأصلية على جهازك",
    "إغلاق النافذة",
    "إغلاق",
    danger: true,
    kind: DialogKind.Warning))
            {
                return;
            }
        }

        _cleanupStarted = true;

        IsEnabled = false;

        ViewModel.StatusMessage =
            "جاري تنظيف الملفات المؤقتة وإغلاق النافذة";

        // تنفيذ التنظيف خارج حدث Closing
        _ = CloseAfterCleanupAsync();
    }

    // تنظيف الملفات ثم إغلاق النافذة
    private async Task CloseAfterCleanupAsync()
    {
        try
        {
            await ViewModel.CleanupAsync();
        }
        catch (Exception ex)
        {
            MessageDialog.Show(
    this,
    $"حدث خطأ أثناء تنظيف الملفات المؤقتة:\n{ex.Message}",
    "تنبيه",
    DialogKind.Warning);
        }
        finally
        {
            _allowClose = true;

            // تنفيذ Close بعد انتهاء حدث Closing الحالي
            _ = Dispatcher.BeginInvoke(new Action(Close));
        }
    }


    // بدء قراءة جميع ملفات PDF المحددة
    private async void StartOcr_Click(
    object sender,
    RoutedEventArgs e)
    {
        await ViewModel.StartOcrAsync();

        UpdateReviewButton();
    }



    // تحديث حالة زر المراجعة عند تغيير الملف المحدد
    private void PendingFilesList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        UpdateReviewButton();
    }

    private void UpdateReviewButton()
    {
        ReviewSelectedButton.IsEnabled =
            !ViewModel.IsBusy &&
            ViewModel.SelectedFile is { IsOcrCompleted: true };
    }

    // فتح شاشة مراجعة الكتاب المحدد
    private async void ReviewSelected_Click(
    object sender,
    RoutedEventArgs e)
    {
        var file = ViewModel.SelectedFile;

        if (file is null || !file.IsOcrCompleted ||
            file.ParsedDraft is null)
        {
            MessageDialog.Show(
    this,
    "يجب إكمال قراءة الملف أولاً",
    "القراءة غير مكتملة",
    DialogKind.Warning);

            return;
        }

        var reviewWindow = new ReviewBookWindow(file)
        {
            Owner = this
        };

        if (reviewWindow.ShowDialog() == true)
        {
            await ViewModel.RemoveSavedAsync(file);
        }

        UpdateReviewButton();
    }

        // إضافة الملفات ثم إخبار المستخدم بالمكرر منها
    private async Task AddAndReportAsync(IEnumerable<string> files)
    {
        var duplicates = await ViewModel.AddFilesAsync(files);

        if (duplicates.Count > 0)
            await ReportDuplicatesAsync(duplicates);
    }

    private async Task ReportDuplicatesAsync(
        List<DuplicateFile> duplicates)
    {
        // عدد كبير: ملخص واحد بدل نوافذ متتالية
        if (duplicates.Count > 3)
        {
            string list = string.Join(
                "\n",
                duplicates.Take(8).Select(x =>
                    $"• {x.FileName}  (كتاب رقم {x.Existing.BookNumberText})"));

            if (duplicates.Count > 8)
                list += $"\nو{duplicates.Count - 8} ملفات أخرى";

            MessageDialog.Show(
                this,
                $"{duplicates.Count} ملفات موجودة بالفعل في الأرشيف، ولم تتم إضافتها:\n\n{list}",
                "ملفات موجودة بالفعل",
                DialogKind.Info);

            return;
        }

        foreach (var item in duplicates)
        {
            var existing = item.Existing;

            // كتاب محذوف سابقاً: نعرض الاستعادة
            if (existing.IsDeleted)
            {
                bool restore = MessageDialog.Confirm(
                    this,
                    $"الملف «{item.FileName}» موجود في الأرشيف ضمن الكتب المحذوفة.\n\n" +
                    $"{existing.DetailsText}\n\nهل تريد استعادة الكتاب؟",
                    "كتاب محذوف سابقاً",
                    "استعادة الكتاب",
                    "تجاهل");

                if (restore)
                {
                    try
                    {
                        await new InspectionBookService()
                            .RestoreAsync(existing.BookId);

                        ViewModel.StatusMessage = "تمت استعادة الكتاب";
                    }
                    catch (Exception ex)
                    {
                        MessageDialog.Show(
                            this, ex.Message,
                            "تعذرت الاستعادة", DialogKind.Error);
                    }
                }

                continue;
            }

            bool edit = MessageDialog.Confirm(
                this,
                $"الكتاب الذي تريد إضافته موجود بالفعل في الأرشيف، ولا حاجة لإضافته مرة أخرى.\n\n" +
                $"الملف: {item.FileName}\n{existing.DetailsText}\n\n" +
                "هل تريد فتح الكتاب الحالي لتعديل بياناته؟",
                "الكتاب موجود بالفعل",
                "تعديل الكتاب الحالي",
                "إغلاق",
                kind: DialogKind.Info);

            if (edit)
            {
                new EditBookWindow(existing.BookId) { Owner = this }
                    .ShowDialog();
            }
        }
    }
}
