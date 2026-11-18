using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DrugInspectionArchive.Controls;

// أيقونات خطية رفيعة (شبكة 24) بأسلوب SF Symbols، تأخذ لون Foreground الموروث
public sealed class Icon : Control
{
    private static readonly Dictionary<string, Geometry> Paths =
        new(StringComparer.OrdinalIgnoreCase);

    static Icon()
    {
        ForegroundProperty.OverrideMetadata(typeof(Icon),
            new FrameworkPropertyMetadata(
                Brushes.Black,
                FrameworkPropertyMetadataOptions.AffectsRender |
                FrameworkPropertyMetadataOptions.Inherits));

        IsHitTestVisibleProperty.OverrideMetadata(typeof(Icon),
            new UIPropertyMetadata(false));

        FocusableProperty.OverrideMetadata(typeof(Icon),
            new FrameworkPropertyMetadata(false));

        Add("M19,11 A8,8 0 1 1 3,11 A8,8 0 1 1 19,11 M21,21 L16.65,16.65",
            "Search", "Magnify");
        Add("M12,5 V19 M5,12 H19", "Plus");
        Add("M18,6 L6,18 M6,6 L18,18", "Close", "X");
        Add("M4,21 V14 M4,10 V3 M12,21 V12 M12,8 V3 M20,21 V16 M20,12 V3 M1,14 H7 M9,8 H15 M17,16 H23",
            "Settings", "Cog");
        Add("M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12 M12,16 V12 M12,8 H12.01",
            "Info", "InformationOutline");
        Add("M14,2 H6 A2,2 0 0 0 4,4 V20 A2,2 0 0 0 6,22 H18 A2,2 0 0 0 20,20 V8 Z M14,2 V8 H20 M16,13 H8 M16,17 H8 M10,9 H8",
            "FileText", "FilePdfBox");
        Add("M14,2 H6 A2,2 0 0 0 4,4 V20 A2,2 0 0 0 6,22 H18 A2,2 0 0 0 20,20 V8 Z M14,2 V8 H20 M12,18 V12 M9,15 H15",
"FilePlus", "FilePlusOutline");
        Add("M17,3 A2.83,2.83 0 1 1 21,7 L7.5,20.5 L2,22 L3.5,16.5 Z",
            "Edit", "Pencil");
        Add("M3,21 H21 M5,21 V5 A1,1 0 0 1 6,4 H14 A1,1 0 0 1 15,5 V21 M15,9 H18 A1,1 0 0 1 19,10 V21 M8,8 H12 M8,12 H12 M8,16 H12",
            "Building", "Domain", "DomainPlus");
        Add("M21,8 V21 H3 V8 M1,3 H23 V8 H1 Z M10,12 H14", "Archive");
        Add("M22,19 A2,2 0 0 1 20,21 H4 A2,2 0 0 1 2,19 V5 A2,2 0 0 1 4,3 H9 L11,6 H20 A2,2 0 0 1 22,8 Z",
            "Folder", "FolderOpenOutline");
        Add("M5,4 H19 A2,2 0 0 1 21,6 V20 A2,2 0 0 1 19,22 H5 A2,2 0 0 1 3,20 V6 A2,2 0 0 1 5,4 Z M16,2 V6 M8,2 V6 M3,10 H21",
            "Calendar");
        Add("M20,6 L9,17 L4,12", "Check", "ContentSave");
        Add("M21,15 V19 A2,2 0 0 1 19,21 H5 A2,2 0 0 1 3,19 V15 M17,8 L12,3 L7,8 M12,3 V15",
            "Upload");
        Add("M1,4 V10 H7 M3.51,15 A9,9 0 1 0 5.64,5.64 L1,10", "Restore");
        Add("M5,3 L19,12 L5,21 Z", "Play");
        Add("M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12 M12,8 V12 M12,16 H12.01",
            "AlertCircle", "AlertCircleOutline");
        Add("M22,11.08 V12 A10,10 0 1 1 15.07,2.76 M22,4 L12,14.01 L9,11.01",
            "CheckCircle", "CheckCircleOutline");
        Add("M10.29,3.86 L1.82,18 A2,2 0 0 0 3.53,21 H20.47 A2,2 0 0 0 22.18,18 L13.71,3.86 A2,2 0 0 0 10.29,3.86 Z M12,9 V13 M12,17 H12.01",
            "AlertTriangle", "AlertOutline");
        Add("M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12 M9.09,9 A3,3 0 0 1 14.99,10 C14.99,12 11.99,13 11.99,13 M12,17 H12.01",
            "HelpCircle", "HelpCircleOutline");
        Add("M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12 M12,6 V12 L16,14",
            "Clock", "ClockOutline");
        Add("M6 9V2h12v7 M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2 M6 14h12v8H6z",
"Printer");
        Add("M18 2a3 3 0 1 0 0 6 3 3 0 0 0 0-6z M6 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z M18 16a3 3 0 1 0 0 6 3 3 0 0 0 0-6z M8.59 13.51l6.83 3.98 M15.41 6.51l-6.82 3.98",
            "Share");
        Add("M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z",
            "Chat", "WhatsApp");
        Add("M6.5 6.5l11 11L12 23V1l5.5 5.5-11 11", "Bluetooth");
        Add("M11 9h9a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2h-9a2 2 0 0 1-2-2v-9a2 2 0 0 1 2-2z M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1",
            "Copy");
        Add("M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0z",
            "Eye");
        // سلة مهملات جديدة بغطاء ومقبض
        Add("M3 6h18 M8 6V4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2 M5 6l1 13a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2l1-13",
            "Trash", "DeleteOutline");

        // تفريغ القائمة
        Add("M16 5H3 M11 12H3 M16 19H3 M21 9l-6 6 M15 9l6 6",
            "ClearAll", "DeleteSweep");

        // إزالة الملف المحدد
        Add("M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z M15 9l-6 6 M9 9l6 6",
            "XCircle");

        Add("M3 6h18 M8 6V4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2 M5 6l1 13a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2l1-13 M10 11l4 4 M14 11l-4 4",
    "TrashX", "DeleteForever");
    
        Add("M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M8.5 10a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z M21 15l-5-5L5 21",
        "Image");
    }

    private static void Add(string data, params string[] names)
    {
        var geometry = Geometry.Parse(data);
        geometry.Freeze();

        foreach (string name in names)
            Paths[name] = geometry;
    }

    public Icon()
    {
        // منع انعكاس الأيقونة في الواجهة من اليمين لليسار
        FlowDirection = FlowDirection.LeftToRight;
    }

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(
            nameof(Kind), typeof(string), typeof(Icon),
            new FrameworkPropertyMetadata(
                string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness), typeof(double), typeof(Icon),
            new FrameworkPropertyMetadata(
                1.8, FrameworkPropertyMetadataOptions.AffectsRender));

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(Math.Min(20, availableSize.Width), Math.Min(20, availableSize.Height));

    protected override void OnRender(DrawingContext dc)
    {
        if (Foreground is null ||
            !Paths.TryGetValue(Kind ?? string.Empty, out var geometry))
            return;

        double size = Math.Min(ActualWidth, ActualHeight);

        if (size <= 0)
            return;

        double scale = size / 24.0;

        dc.PushTransform(new TranslateTransform(
            (ActualWidth - size) / 2, (ActualHeight - size) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));

        var pen = new Pen(Foreground, StrokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        dc.DrawGeometry(null, pen, geometry);

        dc.Pop();
        dc.Pop();
    }
}