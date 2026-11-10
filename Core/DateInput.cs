using System.Globalization;
using System.Text;

namespace DrugInspectionArchive.Core;

public static class DateInput
{
    // قراءة تاريخ مرن: 2025/5/5 أو 5-5-2025 أو أرقام عربية
    public static bool TryParseFlexible(string? text, out DateTime date)
    {
        date = default;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var sb = new StringBuilder();

        foreach (char c in text.Trim())
        {
            sb.Append(c switch
            {
                >= '\u0660' and <= '\u0669' => (char)('0' + c - '\u0660'),
                >= '\u06F0' and <= '\u06F9' => (char)('0' + c - '\u06F0'),
                '-' or '.' or '\\' => '/',
                _ => c
            });
        }

        string[] formats = ["yyyy/M/d", "d/M/yyyy"];

        return DateTime.TryParseExact(
            sb.ToString(),
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }
}