using System.Windows;
using System.Windows.Media;

namespace DrugInspectionArchive.Core;

// لون واحد من ألوان التطبيق
public sealed class AccentOption
{
    public AccentOption(
        string key, string name,
        string main, string hover, string pressed, string soft, string selected)
    {
        Key = key;
        Name = name;
        Main = Parse(main);
        Hover = Parse(hover);
        Pressed = Parse(pressed);
        Soft = Parse(soft);
        Selected = Parse(selected);

        var brush = new SolidColorBrush(Main);
        brush.Freeze();
        Brush = brush;
    }

    public string Key { get; }
    public string Name { get; }
    public Color Main { get; }
    public Color Hover { get; }
    public Color Pressed { get; }
    public Color Soft { get; }
    public Color Selected { get; }
    public Brush Brush { get; }

    private static Color Parse(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex);
}

// ألوان التطبيق الجاهزة وتطبيقها مباشرة بدون إعادة تشغيل
public static class AccentPalette
{
    public const string DefaultKey = "blue";

    public static IReadOnlyList<AccentOption> All { get; } = new[]
    {
        new AccentOption("blue",     "أزرق",    "#0071E3", "#0066CC", "#0058B0", "#E8F1FC", "#E5EFFB"),
        new AccentOption("green",    "أخضر",    "#2E7D5B", "#276B4E", "#205A41", "#E6F2EC", "#DFEEE6"),
        new AccentOption("clay",     "برتقالي", "#B8521A", "#9F4616", "#873B12", "#FBEEE4", "#F8E8DA"),
        new AccentOption("purple",   "بنفسجي",  "#6C5BA5", "#5C4C92", "#4E4079", "#EFECF7", "#E9E5F4"),
        new AccentOption("graphite", "رمادي",   "#3F4650", "#343A43", "#292E36", "#ECEEF1", "#E6E9ED")
    };

    public static AccentOption Find(string? key) =>
        All.FirstOrDefault(o => o.Key == key) ?? All[0];

    public static void Apply(string? key)
    {
        var option = Find(key);
        var resources = Application.Current.Resources;

        Set(resources, "Accent", option.Main);
        Set(resources, "AccentHover", option.Hover);
        Set(resources, "AccentPressed", option.Pressed);
        Set(resources, "AccentSoft", option.Soft);
        Set(resources, "Selected", option.Selected);
    }

    private static void Set(ResourceDictionary resources, string name, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        resources[name + "Color"] = color;
        resources[name + "Brush"] = brush;
    }
}