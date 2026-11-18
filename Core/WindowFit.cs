using System.Windows;

namespace DrugInspectionArchive.Core;

// يضبط حجم النافذة حسب الشاشة، والنوافذ الثانوية حسب النافذة الرئيسية وفي منتصفها
public static class WindowFit
{
    // نسبة حجم النوافذ الثانوية من الرئيسية (غيّرها لتكبير أو تصغير الكل)
    private const double DefaultRatio = 0.75;

    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(WindowFit),
            new PropertyMetadata(false, OnEnabledChanged));

    public static readonly DependencyProperty OwnerRatioProperty =
        DependencyProperty.RegisterAttached(
            "OwnerRatio", typeof(double), typeof(WindowFit),
            new PropertyMetadata(double.NaN));

    public static bool GetEnabled(DependencyObject d) =>
        (bool)d.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject d, bool value) =>
        d.SetValue(EnabledProperty, value);

    public static double GetOwnerRatio(DependencyObject d) =>
        (double)d.GetValue(OwnerRatioProperty);

    public static void SetOwnerRatio(DependencyObject d, double value) =>
        d.SetValue(OwnerRatioProperty, value);

    private static void OnEnabledChanged(
        DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window || e.NewValue is not true)
            return;

        Fit(window);

        window.SourceInitialized += (_, _) =>
        {
            Fit(window);
            CenterOnRoot(window);
        };

        window.Loaded += (_, _) =>
        {
            CenterOnRoot(window);
            KeepInside(window);
        };

        // بعد اكتمال الرسم (للنوافذ ذات الحجم التلقائي)
        window.ContentRendered += (_, _) =>
        {
            CenterOnRoot(window);
            KeepInside(window);
        };
    }

    // النافذة الرئيسية (أعلى سلسلة الملكية)
    private static Window? RootOwner(Window w)
    {
        Window? root = w.Owner;

        while (root?.Owner is { } parent)
            root = parent;

        return root;
    }

    private static bool IsFixedSize(Window w) =>
        w.SizeToContent != SizeToContent.Manual ||
        w.ResizeMode == ResizeMode.NoResize ||
        w.ResizeMode == ResizeMode.CanMinimize;

    private static void Fit(Window w)
    {
        var area = SystemParameters.WorkArea;

        double maxW = area.Width * 0.96;
        double maxH = area.Height * 0.96;

        var root = RootOwner(w);

        if (root is { ActualWidth: > 0, ActualHeight: > 0 })
        {
            maxW = Math.Min(maxW, root.ActualWidth * 0.95);
            maxH = Math.Min(maxH, root.ActualHeight * 0.95);

            double ratio = GetOwnerRatio(w);

            if (double.IsNaN(ratio) && !IsFixedSize(w))
                ratio = DefaultRatio;

            if (!double.IsNaN(ratio))
            {
                w.Width = Math.Min(root.ActualWidth * ratio, maxW);
                w.Height = Math.Min(root.ActualHeight * ratio, maxH);
            }
        }

        if (w.SizeToContent != SizeToContent.Manual)
            w.MaxHeight = Math.Min(w.MaxHeight, maxH);

        if (!double.IsNaN(w.Width)) w.Width = Math.Min(w.Width, maxW);
        if (!double.IsNaN(w.Height)) w.Height = Math.Min(w.Height, maxH);

        w.MinWidth = Math.Min(w.MinWidth, double.IsNaN(w.Width) ? maxW : w.Width);
        w.MinHeight = Math.Min(w.MinHeight, double.IsNaN(w.Height) ? maxH : w.Height);
    }

    // توسيط النافذة داخل النافذة الرئيسية بالحجم النهائي
    private static void CenterOnRoot(Window w)
    {
        var root = RootOwner(w);

        if (root is null || w.WindowState != WindowState.Normal)
            return;

        // نمنع ويندوز من إعادة التوسيط بالحجم القديم
        w.WindowStartupLocation = WindowStartupLocation.Manual;

        double width = w.ActualWidth > 0 ? w.ActualWidth : w.Width;
        double height = w.ActualHeight > 0 ? w.ActualHeight : w.Height;

        if (double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0)
            return;

        var area = SystemParameters.WorkArea;
        bool max = root.WindowState == WindowState.Maximized;

        double rx = max ? area.Left : root.Left;
        double ry = max ? area.Top : root.Top;
        double rw = max ? area.Width : root.ActualWidth;
        double rh = max ? area.Height : root.ActualHeight;

        w.Left = rx + (rw - width) / 2;
        w.Top = ry + (rh - height) / 2;
    }

    // إبقاء النافذة داخل حدود الشاشة
    private static void KeepInside(Window w)
    {
        if (w.WindowState != WindowState.Normal)
            return;

        var area = SystemParameters.WorkArea;

        if (w.Left + w.ActualWidth > area.Right)
            w.Left = Math.Max(area.Left, area.Right - w.ActualWidth);

        if (w.Top + w.ActualHeight > area.Bottom)
            w.Top = Math.Max(area.Top, area.Bottom - w.ActualHeight);

        if (w.Left < area.Left) w.Left = area.Left;
        if (w.Top < area.Top) w.Top = area.Top;
    }
}