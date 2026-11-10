
using System.Globalization;
using System.Text;

namespace DrugInspectionArchive.Services;

public static class TextNormalizer
{
    // توحيد أسماء الشركات والمواد للبحث والمقارنة
    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Normalize(NormalizationForm.FormKC);

        var result = new StringBuilder();
        bool previousWasSpace = false;

        foreach (char c in value)
        {
            // تجاهل التشكيل والتطويل
            if (char.GetUnicodeCategory(c) is
                UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            if (c == '\u0640')
                continue;

            char normalizedChar = c switch
            {
                'أ' or 'إ' or 'آ' => 'ا',
                'ى' => 'ي',
                _ => c
            };

            // توحيد المسافات المتكررة
            if (char.IsWhiteSpace(normalizedChar))
            {
                if (!previousWasSpace && result.Length > 0)
                    result.Append(' ');

                previousWasSpace = true;
                continue;
            }

            result.Append(
                char.ToUpperInvariant(normalizedChar));

            previousWasSpace = false;
        }

        return result.ToString().Trim();
    }

    // توحيد رقم التشغيلة للبحث
    public static string NormalizeBatchNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        value = value.Normalize(NormalizationForm.FormKC);

        var result = new StringBuilder();

        foreach (char c in value)
        {
            // حذف المسافات فقط
            if (char.IsWhiteSpace(c))
                continue;

            result.Append(char.ToUpperInvariant(c));
        }

        // لا نحذف الشرطات أو الشرائح أو الأصفار
        return result.ToString();
    }
}
