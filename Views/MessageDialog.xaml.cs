using Material.Icons;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DrugInspectionArchive.Views;

public enum DialogKind
{
    Info,
    Success,
    Warning,
    Error,
    Question
}

public partial class MessageDialog : Window
{
    private MessageDialog(
        string message,
        string title,
        DialogKind kind,
        bool confirm,
        string okText,
        string cancelText,
        bool danger)
    {
        InitializeComponent();

        TitleText.Text = Clean(title);
        MessageText.Text = Clean(message);

        OkButton.Content = okText;
        CancelButton.Content = cancelText;

        CancelButton.Visibility =
            confirm ? Visibility.Visible : Visibility.Collapsed;

        OkButton.Style = (Style)FindResource(
            danger ? "DangerButton" : "PrimaryButton");

        (MaterialIconKind icon, string colorKey) = kind switch
        {
            DialogKind.Success =>
                (MaterialIconKind.CheckCircleOutline, "SuccessColor"),
            DialogKind.Warning =>
                (MaterialIconKind.AlertOutline, "WarningColor"),
            DialogKind.Error =>
                (MaterialIconKind.AlertCircleOutline, "DangerColor"),
            DialogKind.Question =>
                (MaterialIconKind.HelpCircleOutline, "AccentColor"),
            _ =>
                (MaterialIconKind.InformationOutline, "AccentColor")
        };

        var color = (Color)FindResource(colorKey);

        IconControl.Kind = icon;
        IconControl.Foreground = new SolidColorBrush(color);
        IconHost.Background = new SolidColorBrush(color) { Opacity = 0.12 };
    }

    // رسالة بزر واحد
    public static void Show(
        Window? owner,
        string message,
        string title = "تنبيه",
        DialogKind kind = DialogKind.Info)
    {
        var dialog = new MessageDialog(
            message, title, kind, false, "حسناً", string.Empty, false);

        dialog.Place(owner);
        dialog.ShowDialog();
    }

    // رسالة تأكيد (نعم / لا)
    public static bool Confirm(
        Window? owner,
        string message,
        string title,
        string okText = "متابعة",
        string cancelText = "إلغاء",
        bool danger = false,
        DialogKind kind = DialogKind.Question)
    {
        var dialog = new MessageDialog(
            message, title, kind, true, okText, cancelText, danger);

        dialog.Place(owner);

        return dialog.ShowDialog() == true;
    }

    private void Place(Window? owner)
    {
        if (owner is { IsLoaded: true, IsVisible: true })
        {
            Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    // بدون نقطة بنهاية الرسالة
    private static string Clean(string? text) =>
        (text ?? string.Empty).Trim().TrimEnd('.', ' ');

    private void Ok_Click(object sender, RoutedEventArgs e) =>
        DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) =>
        DialogResult = false;

    private void Header_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Window_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            DialogResult = false;
    }
}