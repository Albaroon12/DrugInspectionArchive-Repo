
using DrugInspectionArchive.Dtos;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DrugInspectionArchive.Services;

public sealed class InspectionBookParser
{
    private static readonly Regex ProductRegex = new(
        @"(?<name>[A-Za-z][A-Za-z0-9()./\- ]{1,55}?\b\d+(?:[.,]\d+)?\s*(?:mcg|meg|µg|μg|ug|mg|ml|iu|g)\b)",
        RegexOptions.IgnoreCase |
        RegexOptions.Compiled);

    private static readonly Regex BatchRegex = new(
        @"^[A-Z0-9]{5,15}$",
        RegexOptions.Compiled);

    private static readonly Regex DateRegex = new(
        @"(?<!\d)(?<date>\d{4}\s*[/.-]\s*\d{1,2}\s*[/.-]\s*\d{1,2}|\d{1,2}\s*[/.-]\s*\d{1,2}\s*[/.-]\s*\d{4})(?!\d)",
        RegexOptions.Compiled);

    public InspectionBookDraft Parse(OcrDocumentResult document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var draft = new InspectionBookDraft
        {
            PageCount = document.PageCount
        };

        draft.CompanyName = ExtractCompany(document);

        draft.BookNumber = ExtractBookNumber(document);

        draft.BookDate = ExtractBookDate(document);

        draft.Materials = ExtractMaterials(document);

        AddWarnings(draft);

        return draft;
    }

    // =========================================
    // COMPANY EXTRACTION
    // =========================================

    private static ExtractedField ExtractCompany(
        OcrDocumentResult document)
    {
        foreach (var page in document.Pages)
        {
            var region = TableExtractor.Locate(page);

            if (region is not null)
            {
                string cell = TableExtractor.ManufacturerText(page, region);

                if (cell.Length >= 5)
                {
                    return new ExtractedField
                    {
                        Value = cell.Length > 90 ? cell[..90] : cell,
                        SourceText = cell,
                        PageNumber = page.PageNumber,
                        Confidence = page.Confidence
                    };
                }
            }
            
            var lines = page.Lines
                .OrderBy(x => x.CenterY)
                .ToList();

            var headerIndex = lines.FindIndex(x =>
                Regex.IsMatch(
                    x.Text,
                    @"manufacturer|الشركة\s*المصنعة|اسم\s*الشركة",
                    RegexOptions.IgnoreCase));

            if (headerIndex < 0)
                continue;

            var header = lines[headerIndex];

            var companyLines = lines
                .Where(x =>
                    x.CenterY > header.CenterY &&
                    x.CenterY < header.CenterY +
                        page.ImageHeight * 0.28 &&
                    GetLineCenterX(x) >
                        page.ImageWidth * 0.68 &&
                    ContainsLatinLetters(x.Text) &&
                    !Regex.IsMatch(
                        x.Text,
                        @"manufacturer|MAH|C\.?O\.?O|Batch",
                        RegexOptions.IgnoreCase))
                .OrderBy(x => x.CenterY)
                .Take(5)
                .ToList();

            if (companyLines.Count == 0)
                continue;

            string value = NormalizeSpaces(
                string.Join(" ", companyLines.Select(x => x.Text)));

            if (value.Length < 2)
                continue;

            return new ExtractedField
            {
                Value = value,
                SourceText = string.Join(
                    Environment.NewLine,
                    companyLines.Select(x => x.Text)),
                PageNumber = page.PageNumber,
                Confidence = companyLines.Average(x => x.Confidence)
            };
        }

        return new ExtractedField();
    }

    // =========================================
    // BOOK NUMBER EXTRACTION
    // =========================================

    private static ExtractedField ExtractBookNumber(
        OcrDocumentResult document)
    {
        var labelRegex = new Regex(
            @"(?:Inspection\s*Drug(?:\s*(?:No|Number))?|Book\s*(?:No|Number)|رقم\s*الكتاب|عدد\s*الكتاب|العدد)",
            RegexOptions.IgnoreCase);

        var valueRegex = new Regex(
            @"(?:Inspection\s*Drug(?:\s*(?:No|Number))?|Book\s*(?:No|Number)|رقم\s*الكتاب|عدد\s*الكتاب|العدد)\s*[:：#\-]?\s*(?<value>[A-Z0-9][A-Z0-9/\-]{0,19})",
            RegexOptions.IgnoreCase);

        foreach (var page in document.Pages)
        {
            foreach (var line in page.Lines.OrderBy(x => x.CenterY))
            {
                // رقم الكتاب غالباً في رأس الصفحة
                if (line.CenterY > page.ImageHeight * 0.38)
                    continue;

                string text = NormalizeDigits(line.Text);

                var match = valueRegex.Match(text);

                if (match.Success)
                {
                    string value = match.Groups["value"].Value;

                    // استبعاد التاريخ إذا التقطه OCR بعد التسمية
                    if (DateRegex.IsMatch(value))
                        continue;

                    return new ExtractedField
                    {
                        Value = value.Trim(),
                        SourceText = line.Text,
                        PageNumber = page.PageNumber,
                        Confidence = line.Confidence
                    };
                }
            }
        }

        // لا نستخرج رقماً من عبارة "المرقم" في المراسلات
        return new ExtractedField();
    }

    // =========================================
    // BOOK DATE EXTRACTION
    // =========================================

    private static ExtractedField ExtractBookDate(
        OcrDocumentResult document)
    {
        var dateLabels = new Regex(
            @"تاريخ\s*الكتاب|التاريخ|تاريخه|(?:Book\s*)?Date|Dated",
            RegexOptions.IgnoreCase);

        foreach (var page in document.Pages)
        {
            var headerLines = page.Lines
                .Where(x => x.CenterY < page.ImageHeight * 0.38)
                .OrderBy(x => x.CenterY)
                .ToList();

            for (int i = 0; i < headerLines.Count; i++)
            {
                if (!dateLabels.IsMatch(headerLines[i].Text))
                    continue;

                // البحث في السطر نفسه والأسطر القريبة فقط
                var nearbyLines = headerLines
                    .Where(x =>
                        Math.Abs(x.CenterY -
                                 headerLines[i].CenterY) <
                        page.ImageHeight * 0.07)
                    .ToList();

                foreach (var line in nearbyLines)
                {
                    string normalized =
                        NormalizeDigits(line.Text);

                    var match = DateRegex.Match(normalized);

                    if (!match.Success)
                        continue;

                    string rawDate = match.Groups["date"].Value;

                    if (!TryParseDate(rawDate, out DateOnly parsedDate))
                        continue;

                    return new ExtractedField
                    {
                        Value = parsedDate.ToString(
                            "yyyy/MM/dd",
                            CultureInfo.InvariantCulture),

                        SourceText = line.Text,

                        PageNumber = page.PageNumber,

                        Confidence = line.Confidence
                    };
                }
            }
        }

        return new ExtractedField();
    }

    // =========================================
    // MATERIAL AND BATCH EXTRACTION
    // =========================================

        private static List<ExtractedMaterialCandidate>
        ExtractMaterials(OcrDocumentResult document)
    {
        var results = new List<ExtractedMaterialCandidate>();

        foreach (var page in document.Pages)
        {
            var region = TableExtractor.Locate(page);

            if (region is null)
                continue;

            results.AddRange(TableExtractor.Extract(page, region));
        }

        // تعليم التكرار: نفس المادة ونفس Batch أكثر من مرة
        var duplicates = results
            .Where(x => !string.IsNullOrWhiteSpace(x.MaterialName) &&
                        !string.IsNullOrWhiteSpace(x.BatchNumber))
            .GroupBy(x => new
            {
                Material = TextNormalizer.NormalizeName(x.MaterialName),
                Batch = TextNormalizer.NormalizeBatchNumber(x.BatchNumber)
            })
            .Where(g => g.Count() > 1);

        foreach (var group in duplicates)
        {
            foreach (var item in group)
                item.HasDuplicateEntry = true;
        }

        return results;
    }

    // =========================================
    // WARNINGS
    // =========================================

    private static void AddWarnings(InspectionBookDraft draft)
    {
        if (!draft.CompanyName.IsFound)
            draft.Warnings.Add(
                "لم يتم التعرف على اسم الشركة بشكل واضح.");

        if (!draft.BookNumber.IsFound)
            draft.Warnings.Add(
                "رقم الكتاب يحتاج إلى إدخال أو مراجعة يدوية.");

        if (!draft.BookDate.IsFound)
            draft.Warnings.Add(
                "تاريخ الكتاب لم يُستخرج بشكل مؤكد.");

        if (draft.Materials.Count == 0)
            draft.Warnings.Add(
                "لم يتم التعرف على مواد أو أرقام تشغيلات.");

        if (draft.Materials.Any(
            x => string.IsNullOrWhiteSpace(x.BatchNumber)))
        {
            draft.Warnings.Add(
                "هناك مواد لم يتم التعرف على Batch Number الخاص بها.");
        }

        if (draft.Materials.Any(x => x.HasDuplicateEntry))
        {
            draft.Warnings.Add(
                "يوجد سجل مكرر لنفس المادة ونفس Batch Number.");
        }
    }

    // =========================================
    // HELPERS
    // =========================================

    private static int GetLineCenterX(OcrLineResult line)
    {
        if (line.X1.HasValue && line.X2.HasValue)
        {
            return (line.X1.Value + line.X2.Value) / 2;
        }

        return line.Words.Count > 0
            ? (int)line.Words.Average(x => x.CenterX)
            : 0;
    }

    private static bool ContainsLatinLetters(string value)
    {
        return Regex.IsMatch(value, @"[A-Za-z]");
    }

    private static string NormalizeSpaces(string value)
    {
        return Regex.Replace(value.Trim(), @"\s+", " ");
    }

    private static string NormalizeBatchToken(string value)
    {
        return value.Trim()
            .Trim('.', ',', ':', ';', '-', '_', '|')
            .ToUpperInvariant();
    }

    private static bool IsValidBatchToken(string value)
    {
        return BatchRegex.IsMatch(value) &&
               value.Any(char.IsLetter) &&
               value.Any(char.IsDigit);
    }

    private static string NormalizeDigits(string value)
    {
        var result = new StringBuilder();

        foreach (char c in value)
        {
            char converted = c switch
            {
                >= '\u0660' and <= '\u0669'
                    => (char)('0' + c - '\u0660'),

                >= '\u06F0' and <= '\u06F9'
                    => (char)('0' + c - '\u06F0'),

                _ => c
            };

            result.Append(converted);
        }

        return result.ToString();
    }

    private static bool TryParseDate(
        string value,
        out DateOnly date)
    {
        string normalized = Regex.Replace(
            NormalizeDigits(value),
            @"\s+",
            "");

        string[] formats =
        [
            "yyyy/M/d",
            "yyyy/MM/dd",
            "yyyy-M-d",
            "yyyy-MM-dd",
            "d/M/yyyy",
            "dd/MM/yyyy",
            "d-M-yyyy",
            "dd-MM-yyyy"
        ];

        return DateOnly.TryParseExact(
            normalized,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    private sealed class ProductLine
    {
        public string Name { get; init; } = string.Empty;

        public int CenterY { get; init; }

        public float Confidence { get; init; }

        public string SourceText { get; init; } = string.Empty;
    }
}
