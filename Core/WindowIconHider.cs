using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DrugInspectionArchive.Core;

// إخفاء أيقونة وعنوان إطار النافذة، وفرض أيقونة التطبيق في شريط المهام
public static class WindowIconHider
{
    // غيّر المسار إذا كان الملف في Assets/Icons
    private const string IconUri = "pack://application:,,,/Assets/icons/App.ico";

    public static readonly DependencyProperty HideProperty =
        DependencyProperty.RegisterAttached(
            "Hide", typeof(bool), typeof(WindowIconHider),
            new PropertyMetadata(false, OnHideChanged));

    public static bool GetHide(DependencyObject d) => (bool)d.GetValue(HideProperty);

    public static void SetHide(DependencyObject d, bool v) => d.SetValue(HideProperty, v);

    private static void OnHideChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
               if (d is not Window w || e.NewValue is not true)
            return;

        // أيقونة النافذة (تظهر في شريط المهام)
        try
        {
            w.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri("pack://application:,,,/Assets/Icons/App.ico", UriKind.Absolute));
        }
        catch (Exception ex)
        {
            MessageBox.Show("تعذر تحميل أيقونة التطبيق\n" + ex.Message, "Icon error");
        }

        w.SourceInitialized += (_, _) => Apply(w);
    }

    private static void Apply(Window w)
    {
        w.Title = string.Empty;

        IntPtr hwnd = new WindowInteropHelper(w).Handle;

        // إخفاء الأيقونة من شريط عنوان النافذة
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_DLGMODALFRAME);

        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
    }

    private static System.Drawing.Icon Load(int size)
    {
        var info = Application.GetResourceStream(new Uri(IconUri))
                   ?? throw new FileNotFoundException("Resource not found: " + IconUri);

        using var stream = info.Stream;
        return new System.Drawing.Icon(stream, new System.Drawing.Size(size, size));
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_DLGMODALFRAME = 0x0001;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int value);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}