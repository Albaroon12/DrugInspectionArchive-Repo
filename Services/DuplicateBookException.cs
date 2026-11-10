namespace DrugInspectionArchive.Services;

// ملخص كتاب موجود في الأرشيف
public sealed class ExistingBookSummary
{
    public int BookId { get; init; }
    public int CompanyId { get; init; }
    public string PdfRelativePath { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public string? BookNumber { get; init; }
    public DateOnly? BookDate { get; init; }
    public int MaterialCount { get; init; }
    public bool IsDeleted { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;

    // true: نفس الملف (نفس التوقيع) / false: ملف مختلف بنفس المحتوى
    public bool SameFile { get; init; }

    public string BookNumberText =>
        string.IsNullOrWhiteSpace(BookNumber) ? "غير محدد" : BookNumber;

    public string BookDateText =>
        BookDate?.ToString("yyyy/MM/dd") ?? "غير محدد";

    public string DetailsText =>
        $"الشركة: {CompanyName}\n" +
        $"رقم الكتاب: {BookNumberText}\n" +
        $"تاريخ الكتاب: {BookDateText}\n" +
        $"عدد المواد: {MaterialCount}";
}

// ملف مكرر مع اسمه كما اختاره المستخدم
public sealed record DuplicateFile(
    string FileName,
    ExistingBookSummary Existing);

// يُرمى عند محاولة إضافة كتاب موجود بالفعل
public sealed class DuplicateBookException : InvalidOperationException
{
    public ExistingBookSummary Existing { get; }

    public DuplicateBookException(ExistingBookSummary existing)
        : base(BuildMessage(existing))
    {
        Existing = existing;
    }

    private static string BuildMessage(ExistingBookSummary e)
    {
        if (e.SameFile && e.IsDeleted)
        {
            return "هذا الملف موجود في الأرشيف ضمن الكتب المحذوفة.\n\n" +
                   e.DetailsText;
        }

        if (e.SameFile)
        {
            return "هذا الملف موجود بالفعل في الأرشيف، ولا حاجة لإضافته مرة أخرى.\n\n" +
                   e.DetailsText;
        }

        return "يوجد في الأرشيف كتاب بنفس رقم الكتاب ونفس المواد وأرقام التشغيلات، " +
               "وإن اختلف الملف المرفوع عنه. لا حاجة لإضافته مرة أخرى.\n\n" +
               e.DetailsText;
    }
}

// نتيجة مقارنة كتاب جديد بكتاب موجود برقم الكتاب نفسه
public sealed class SimilarBookMatch
{
    public ExistingBookSummary Existing { get; init; } = null!;
    public List<string> Differences { get; init; } = [];
    public bool IsExact => Differences.Count == 0;
}