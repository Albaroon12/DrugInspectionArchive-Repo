using System.Text.RegularExpressions;
using DrugInspectionArchive.Dtos;

namespace DrugInspectionArchive.Services;

// موقع جدول المواد داخل صفحة الكتاب (بإحداثيات صورة OCR)
public sealed class TableRegion
{
    public int TableLeft { get; init; }
    public int BatchLeft { get; init; }
    public int MfgLeft { get; init; }
    public int ManufacturerLeft { get; init; }
    public int Top { get; init; }
    public int Bottom { get; init; }
    public List<OcrWordResult> Batches { get; init; } = [];

    // مواضع (Y) صفوف الجدول المستنتجة من أعمدة التواريخ
    public List<int> DateRows { get; init; } = [];
}

// قراءة جدول (المنتج / Batch / التواريخ / الكمية) اعتماداً على إحداثيات الكلمات
public static class TableExtractor
{
    private static readonly Regex DateRx = new(
        @"^(0?[1-9]|1[0-2])\s*/\s*(20\d\d)$",
        RegexOptions.Compiled);

    private static readonly Regex BatchRx = new(
        @"^[A-Z0-9][A-Z0-9\-/]{3,14}$",
        RegexOptions.Compiled);

    // كلمات تشبه الجرعة أو التعبئة وليست رقم تشغيلة
    private static readonly Regex UnitLikeRx = new(
        @"^\d+[.,]?\d*(MG|MCG|MEG|ML|G|IU|KG|%)$|BLISTER|FILM|TABLET|CAPSULE|BOTTLE|PACK|VIAL|AMP|SYRUP|SUSP",
        RegexOptions.Compiled);

        // سطر يحتوي الحجم أو الجرعة فقط (مثل 500ml) ويكمل اسم المادة
    private static readonly Regex VolumeLineRx = new(
        @"^\d+(?:[.,]\d+)?\s*(ml|m[l!|1]|mg|mcg|g|iu)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex StrengthRx = new(
        @"\d+(?:[.,]\d+)?\s*(?:mcg|meg|mg|ml|g|iu|%)(?:\s*/\s*\d*\s*ml)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PackWordRx = new(
        @"pack|bottle|box|vial|ampoule|tube|sachet|blister",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TerminalRx = new(
        @"\b(pack|packs|bottle|bottles|box|boxes|vial|vials|ampoule|ampoules|tube|tubes|sachet|sachets|blister|blisters|strip|strips|carton|cartons)\b.*\b(of|x|×)\b|\b(pack|bottle|box|vial|ampoule|tube|sachet)\s*of\b|\d\s*[x×]\s*\d+\s*(tablet|capsule|ml|amp)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NoiseRx = new(
        @"^[\W_]*$",
        RegexOptions.Compiled);

    private static readonly Regex LabelWordRx = new(
        @"^(manufacturer|manufacture|manutactire|manufactrer|mah|mal|mat|c\.?o\.?o|and|packager|bulk|manufacturer&|manufactrer&|&)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly char[] TrimChars =
        [' ', '|', '.', ',', ':', ';', '\'', '"', '“', '”', '‘', '’', '`', '-', '_', '—', '='];

    private static int X1(OcrWordResult w) => w.X1 ?? 0;
    private static int X2(OcrWordResult w) => w.X2 ?? 0;

    private static string Clean(string text) => text.Trim(TrimChars);

    private static float Pct(float c) =>
        float.IsFinite(c) ? (c <= 1f ? c * 100f : c) : 0f;

    private static int Median(IEnumerable<int> values)
    {
        var list = values.OrderBy(x => x).ToList();
        return list.Count == 0 ? 0 : list[list.Count / 2];
    }

    // رقم تشغيلة سليم الشكل
    private static string? ValidBatch(string text)
    {
        string token = Clean(text).ToUpperInvariant();

        if (!BatchRx.IsMatch(token) ||
            !token.Any(char.IsDigit) ||
            DateRx.IsMatch(token) ||
            UnitLikeRx.IsMatch(token))
            return null;

        return token;
    }

    // رقم تشغيلة مشوّه (فيه حرف عربي أو رمز): نأخذ الأحرف اللاتينية والأرقام فقط
    private static string? RescuedBatch(string text)
    {
        var chars = text
            .Where(c => c < 128 &&
                        (char.IsLetterOrDigit(c) || c == '-' || c == '/'))
            .ToArray();

        string token = new string(chars).ToUpperInvariant();

        if (token.Length < 4 ||
            token.Length > 15 ||
            !token.Any(char.IsDigit) ||
            !BatchRx.IsMatch(token) ||
            DateRx.IsMatch(token) ||
            UnitLikeRx.IsMatch(token))
            return null;

        return token;
    }

    private static string BatchText(string text) =>
        ValidBatch(text) ?? RescuedBatch(text) ?? string.Empty;

    // =====================================================
    // تحديد منطقة الجدول من أعمدة التواريخ و Batch
    // =====================================================
    public static TableRegion? Locate(OcrPageResult page)
    {
        int h = page.ImageHeight;
        int w = page.ImageWidth;

        if (h <= 0 || w <= 0)
            return null;

        var dates = page.Words
            .Where(x => DateRx.IsMatch(Clean(x.Text)) &&
                        x.CenterY > h * 0.25 &&
                        x.CenterY < h * 0.8)
            .OrderBy(x => x.CenterY)
            .ToList();

        if (dates.Count == 0)
            return null;

        // التواريخ المتصلة رأسياً فقط (نستبعد ملصق العينة أسفل الصفحة)
        var kept = new List<OcrWordResult> { dates[0] };

        foreach (var d in dates.Skip(1))
        {
            if (d.CenterY - kept[^1].CenterY > 400)
                break;

            kept.Add(d);
        }

        // صفوف الجدول حسب مواضع التواريخ
        var dateRows = new List<int>();

        foreach (var d in kept.OrderBy(x => x.CenterY))
        {
            if (dateRows.Count > 0 && d.CenterY - dateRows[^1] <= 40)
                continue;

            dateRows.Add(d.CenterY);
        }

        var byX = kept.OrderBy(X1).ToList();

        int split = -1;
        int bestGap = 120;

        for (int i = 0; i < byX.Count - 1; i++)
        {
            int gap = X1(byX[i + 1]) - X1(byX[i]);

            if (gap > bestGap)
            {
                bestGap = gap;
                split = i;
            }
        }

        var leftGroup = split >= 0 ? byX.Take(split + 1) : byX;

        int mfgLeft = Median(leftGroup.Select(X1));

        int yMin = kept.Min(x => x.CenterY) - 120;
        int yMax = kept.Max(x => x.CenterY) + 150;

        // كلمة Batch في رأس الجدول (مرجع موقع العمود إن وُجدت)
        var batchHeader = page.Words
            .Where(x => Regex.IsMatch(
                            Clean(x.Text), @"^Batch$",
                            RegexOptions.IgnoreCase) &&
                        x.CenterY > h * 0.25 &&
                        x.CenterY > yMin - 200 &&
                        x.CenterY < yMin + 250)
            .OrderBy(x => Math.Abs(x.CenterY - yMin))
            .FirstOrDefault();

        var candidates = page.Words
            .Where(x => x.CenterX < mfgLeft - 15 &&
                        x.CenterY > yMin &&
                        x.CenterY < yMax &&
                        ValidBatch(x.Text) is not null &&
                        (batchHeader is null ||
                         Math.Abs(x.CenterX - batchHeader.CenterX) <= 130))
            .ToList();

        if (candidates.Count == 0)
            return null;

        // عمود Batch = أكبر مجموعة كلمات متقاربة أفقياً (الأيمن عند التعادل)
        var column = new List<OcrWordResult>();

        foreach (var c in candidates.OrderBy(x => x.CenterX))
        {
            var group = candidates
                .Where(o => Math.Abs(o.CenterX - c.CenterX) <= 70)
                .ToList();

            if (group.Count >= column.Count)
                column = group;
        }

        int columnCenter = Median(column.Select(x => x.CenterX));

        // إنقاذ أرقام التشغيلة المشوّهة داخل نفس العمود
        var rescued = page.Words
            .Where(x => x.CenterY > yMin &&
                        x.CenterY < yMax &&
                        Math.Abs(x.CenterX - columnCenter) <= 70 &&
                        ValidBatch(x.Text) is null &&
                        RescuedBatch(x.Text) is not null &&
                        !column.Any(c => Math.Abs(c.CenterY - x.CenterY) <= 25))
            .ToList();

        var batches = column
            .Concat(rescued)
            .OrderBy(x => x.CenterY)
            .ToList();

        int batchLeft = Median(batches.Select(X1));

        int bottom = Math.Max(
            batches.Max(x => x.CenterY),
            kept.Max(x => x.CenterY)) + 190;

        var header = page.Words
            .Where(x => Regex.IsMatch(
                            x.Text,
                            @"product|packaging|name&",
                            RegexOptions.IgnoreCase) &&
                        x.CenterY < yMin + 200 &&
                        x.CenterY > h * 0.25)
            .ToList();

        int top = header.Count > 0
            ? header.Max(x => x.Y2 ?? 0) + 60
            : yMin;

        int tableLeft = header.Count > 0
            ? Math.Max(0, header.Min(X1) - 140)
            : Math.Max(0, batchLeft - (int)(w * 0.27));

        var packWords = page.Words
            .Where(x => x.CenterX > mfgLeft &&
                        x.CenterY > top &&
                        x.CenterY < bottom &&
                        Regex.IsMatch(
                            x.Text,
                            @"^(packs?|bottles?|vials?|boxes|box|ampoules?)$",
                            RegexOptions.IgnoreCase))
            .ToList();

        int manufacturerLeft = packWords.Count > 0
            ? packWords.Max(X2) + 15
            : (int)(w * 0.75);

        return new TableRegion
        {
            TableLeft = tableLeft,
            BatchLeft = batchLeft,
            MfgLeft = mfgLeft,
            ManufacturerLeft = manufacturerLeft,
            Top = Math.Max(0, top),
            Bottom = Math.Min(h, bottom),
            Batches = batches,
            DateRows = dateRows
        };
    }

    // =====================================================
    // اسم الشركة المصنعة من خلية Manufacturer
    // =====================================================
    public static string ManufacturerText(
        OcrPageResult page,
        TableRegion region)
    {
        var words = page.Words
            .Where(x => x.CenterX > region.ManufacturerLeft &&
                        x.CenterY > region.Top &&
                        x.CenterY < region.Bottom &&
                        Pct(x.Confidence) >= 55 &&
                        Regex.IsMatch(x.Text, @"[A-Za-z]{2,}"))
            .OrderBy(x => x.CenterY / 30)
            .ThenBy(X1)
            .Select(x => Clean(x.Text))
            .Where(t => t.Length > 0 && !LabelWordRx.IsMatch(t))
            .ToList();

        return Regex.Replace(string.Join(" ", words), @"\s+", " ").Trim();
    }

    // =====================================================
    // استخراج (مادة، Batch) لكل صف
    // =====================================================
    public static List<ExtractedMaterialCandidate> Extract(
        OcrPageResult page,
        TableRegion region)
    {
        var results = new List<ExtractedMaterialCandidate>();

        // صفوف الأرقام المقروءة + صفوف فارغة حيث ظهر تاريخ بلا رقم تشغيلة
        var rows = region.Batches
            .Select(b => (Y: b.CenterY,
                          Word: (OcrWordResult?)b,
                          Text: BatchText(b.Text)))
            .ToList();

        foreach (var anchor in region.DateRows)
        {
            if (!rows.Any(r => Math.Abs(r.Y - anchor) <= 40))
                rows.Add((Y: anchor, Word: null, Text: string.Empty));
        }

        rows = rows.OrderBy(r => r.Y).ToList();

        // كلمات عمود المنتج: من إعادة القراءة الإنجليزية إن وجدت
        IEnumerable<OcrWordResult> productWords =
            page.ProductWords.Count > 0
                ? page.ProductWords
                : page.Words.Where(x =>
                    x.CenterX < region.BatchLeft - 12 &&
                    X1(x) >= region.TableLeft &&
                    x.CenterY > region.Top &&
                    x.CenterY < region.Bottom);

        var lines = BuildLines(productWords);

        var blocks = BuildBlocks(lines);

        int lastRowY = rows.Max(r => r.Y);

        // لا يبدأ اسم مادة أسفل آخر صف
        blocks = blocks
            .Where(b => b[0].CenterY <= lastRowY + 60)
            .ToList();

        var names = blocks.Select(NameOf).ToList();

        var bounds = new List<int>();

        for (int i = 0; i < blocks.Count - 1; i++)
        {
            bounds.Add(
                (blocks[i][^1].CenterY + blocks[i + 1][0].CenterY) / 2);
        }

        foreach (var row in rows)
        {
            int blockIndex = blocks.Count - 1;

            for (int k = 0; k < bounds.Count; k++)
            {
                if (row.Y < bounds[k])
                {
                    blockIndex = k;
                    break;
                }
            }

            string name = blockIndex >= 0 ? names[blockIndex] : string.Empty;

            string source = blockIndex >= 0
                ? string.Join(" / ", blocks[blockIndex].Select(l => l.Text))
                : string.Empty;

            float confidence = row.Word is null
                ? 0f
                : Pct(row.Word.Confidence);

            if (blockIndex >= 0)
                confidence = Math.Min(
                    confidence, blocks[blockIndex][0].Confidence);

            results.Add(new ExtractedMaterialCandidate
            {
                MaterialName = name,
                BatchNumber = row.Text,
                PageNumber = page.PageNumber,
                Confidence = confidence,
                SourceText = row.Word is null
                    ? $"{source} | (لم تتم قراءة رقم التشغيلة)"
                    : $"{source} | {row.Word.Text}",
                NeedsReview = true
            });
        }

        return results;
    }

    private sealed class Line
    {
        public int CenterY { get; set; }
        public float Confidence { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<OcrWordResult> Words { get; } = [];
    }

    private static List<Line> BuildLines(IEnumerable<OcrWordResult> words)
    {
        var usable = words
            .Where(x => !NoiseRx.IsMatch(x.Text) &&
                        Clean(x.Text).Length > 0)
            .OrderBy(x => x.CenterY)
            .ToList();

        var lines = new List<Line>();

        foreach (var word in usable)
        {
            if (lines.Count > 0 &&
                Math.Abs(lines[^1].CenterY - word.CenterY) <= 22)
            {
                lines[^1].Words.Add(word);
                lines[^1].CenterY =
                    (int)lines[^1].Words.Average(x => x.CenterY);
            }
            else
            {
                var line = new Line { CenterY = word.CenterY };
                line.Words.Add(word);
                lines.Add(line);
            }
        }

        foreach (var line in lines)
        {
            line.Words.Sort((a, b) => X1(a).CompareTo(X1(b)));

            line.Text = Regex.Replace(
                string.Join(" ", line.Words.Select(x => Clean(x.Text))),
                @"\s+", " ").Trim();

            line.Confidence = line.Words.Max(x => Pct(x.Confidence));
        }

        // حذف أسطر الضجيج (حدود الجدول، بقع الماسح)
        return lines
            .Where(l => l.Text.Length >= 2 &&
                        Regex.IsMatch(l.Text, @"[A-Za-z0-9]") &&
                        (l.Confidence >= 50 ||
                         StrengthRx.IsMatch(l.Text) ||
                         PackWordRx.IsMatch(l.Text)))
            .ToList();
    }

    private static bool IsContinuation(string text) =>
        text.Length > 0 &&
        (char.IsLower(text[0]) ||
         Regex.IsMatch(text, @"^\d+\s*(ml|m[l!|1]|mg|g)\b",
             RegexOptions.IgnoreCase));

    // تقسيم الأسطر إلى خلايا منتجات
    private static List<List<Line>> BuildBlocks(List<Line> lines)
    {
        var blocks = new List<List<Line>>();

        if (lines.Count == 0)
            return blocks;

        var gaps = new List<int>();

        for (int i = 1; i < lines.Count; i++)
            gaps.Add(lines[i].CenterY - lines[i - 1].CenterY);

        double median = gaps.Count == 0
            ? 45
            : gaps.OrderBy(x => x).ElementAt(gaps.Count / 2);

        bool closed = false;

        foreach (var line in lines)
        {
            if (blocks.Count == 0)
            {
                blocks.Add([line]);
                closed = TerminalRx.IsMatch(line.Text);
                continue;
            }

            var previous = blocks[^1][^1];

            bool startNew =
                (closed && !IsContinuation(line.Text)) ||
                line.CenterY - previous.CenterY > median * 2.2;

            if (startNew)
            {
                blocks.Add([line]);
                closed = TerminalRx.IsMatch(line.Text);
            }
            else
            {
                blocks[^1].Add(line);

                if (TerminalRx.IsMatch(line.Text))
                    closed = true;
            }
        }

        return blocks;
    }

    // اسم المادة = أول سطر (الاسم التجاري) + الجرعة. لا نأخذ الاسم العلمي.
    // إذا لم تكن الجرعة بالسطر الأول، نأخذها فقط إن وُجدت جرعة واحدة واضحة
    // في بقية الخلية (باستثناء سطور التعبئة مثل Pack of / Bottle of).
    private static string NameOf(List<Line> block)
    {
        string first = block[0].Text;

        // السطر الثاني إذا كان حجماً أو جرعة فقط يكمل الاسم
        string suffix =
            block.Count > 1 && VolumeLineRx.IsMatch(block[1].Text)
                ? " " + block[1].Text
                : string.Empty;

        if (suffix.Length > 0 || StrengthRx.IsMatch(first))
            return FixName(first + suffix);

        if (StrengthRx.IsMatch(first))
            return FixName(first);

        var strengths = block
            .Skip(1)
            .Where(l => !PackWordRx.IsMatch(l.Text) &&
                        !TerminalRx.IsMatch(l.Text))
            .SelectMany(l => StrengthRx.Matches(l.Text)
                .Select(m => m.Value))
            .GroupBy(v => Regex.Replace(v, @"\s+", "")
                .ToLowerInvariant())
            .Select(g => g.First())
            .ToList();

        return strengths.Count == 1
            ? FixName(first + " " + strengths[0])
            : FixName(first);
    }

    private static string FixName(string value)
    {
        value = Regex.Replace(value, @"(?<=\d)\|(?=\d)", "");

        // OCR يقرأ ml أحياناً على أنها m! أو m1
        value = Regex.Replace(value, @"(\d)\s*m[!|1](?!\w)", "$1 ml");
        
        value = Regex.Replace(
            value,
            @"([A-Za-z])(\d+(?:[.,]\d+)?\s*(?:mcg|meg|mg|ml|g|iu)\b)",
            "$1 $2",
            RegexOptions.IgnoreCase);

        value = Regex.Replace(
            value, @"(\d)\s*meg\b", "$1 mcg",
            RegexOptions.IgnoreCase);

        value = Regex.Replace(
            value, @"(\d)\s*(mg|mcg|ml|g)\b", "$1 $2",
            RegexOptions.IgnoreCase);

        return Regex.Replace(value, @"\s+", " ").Trim();
    }
}