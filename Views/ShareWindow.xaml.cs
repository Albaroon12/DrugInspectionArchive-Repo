using DrugInspectionArchive.Services;
using System.ComponentModel;
using System.Windows;

namespace DrugInspectionArchive.Views;

public partial class ShareWindow : Window
{
    private readonly ArchiveStorageService _storage = new();
    private readonly ShareService _share = new();

    private readonly string _relativePath;
    private readonly string _label;

    public ShareWindow(string relativePath, string label)
    {
        InitializeComponent();

        _relativePath = relativePath;
        _label = label;

        LabelText.Text = label;
    }

    // نسخة مؤقتة من الملف جاهزة للمشاركة
    private string PrepareFile() =>
        _share.PrepareCopy(_storage.ResolveFullPath(_relativePath));

    private void SetStatus(string message, bool success = true)
    {
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(
            success ? "SuccessBrush" : "DangerBrush");

        StatusText.Text = message;
    }

    private void WhatsApp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _share.CopyFileToClipboard(PrepareFile());
            _share.OpenWhatsApp(_label);

            SetStatus("تم نسخ الملف، افتح المحادثة المطلوبة في واتساب ثم اضغط Ctrl+V");
        }
        catch (Win32Exception)
        {
            SetStatus(
                "تعذر فتح واتساب، تأكد من تثبيت واتساب لسطح المكتب (الملف منسوخ ويمكنك لصقه)",
                false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
        }
    }

    private void Bluetooth_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _share.SendBluetooth(PrepareFile());

            SetStatus("تم فتح معالج بلوتوث، اختر الجهاز المقترن لإرسال الملف");
        }
        catch (Win32Exception)
        {
            SetStatus("تعذر فتح معالج بلوتوث، تأكد من تشغيل بلوتوث في جهازك", false);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _share.CopyFileToClipboard(PrepareFile());

            SetStatus("تم نسخ الملف، يمكنك لصقه في أي تطبيق");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
        }
    }

    private void Folder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _storage.ShowInFolder(_relativePath);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, false);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}