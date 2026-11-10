using System.Globalization;
using System.Text.RegularExpressions;

namespace DrugInspectionArchive.Core;

public static class MaterialSorter
{
    private static readonly Regex DoseRx = new(
        @"(\d+(?:[.,]\d+)?)\s*(mcg|µg|μg|ug|mg|g|ml|iu|%)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // المواد بترتيب ظهورها، وداخل المادة الواحدة الجرعة من الأكبر للأصغر
    public static List<T> Sort<T>(
        IEnumerable<T> items,
        Func<T, string> name,
        Func<T, string> batch)
    {
        var groupOrder = new Dictionary<string, int>();
        var parsed = new List<(T Item, string Key, double Dose)>();

        foreach (var item in items)
        {
            var (key, dose) = Parse(name(item));

            if (!groupOrder.ContainsKey(key))
                groupOrder[key] = groupOrder.Count;

            parsed.Add((item, key, dose));
        }

        return parsed
            .OrderBy(x => groupOrder[x.Key])
            .ThenByDescending(x => x.Dose)
            .ThenBy(x => name(x.Item), StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => batch(x.Item), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Item)
            .ToList();
    }

    private static (string Key, double Dose) Parse(string? name)
    {
        string text = (name ?? string.Empty).Trim();
        var match = DoseRx.Match(text);

        if (!match.Success)
            return (text.ToUpperInvariant(), 0);

        string key = text[..match.Index].Trim().ToUpperInvariant();

        if (key.Length == 0)
            key = text.ToUpperInvariant();

        double value = double.Parse(
            match.Groups[1].Value.Replace(',', '.'),
            CultureInfo.InvariantCulture);

        double factor = match.Groups[2].Value.ToLowerInvariant() switch
        {
            "mcg" or "µg" or "μg" or "ug" => 0.001,
            "g" => 1000,
            _ => 1
        };

        return (key, value * factor);
    }
}