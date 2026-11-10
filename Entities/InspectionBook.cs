
using DrugInspectionArchive.Enums;

namespace DrugInspectionArchive.Entities;

public sealed class InspectionBook
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    // رقم الكتاب المستخرج من المسح الضوئي
    public string? BookNumber { get; set; }

    // نسخة موحدة للبحث والترتيب
    public string? NormalizedBookNumber { get; set; }

    // تاريخ إصدار الكتاب نفسه
    public DateOnly? BookDate { get; set; }

    // تاريخ إضافة الكتاب إلى الأرشيف
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    // اسم الملف الأصلي
    public string OriginalFileName { get; set; } = string.Empty;

    // مسار PDF النسبي داخل مجلد الأرشيف
    public string PdfRelativePath { get; set; } = string.Empty;

    // بصمة الملف SHA-256
    public string? FileSha256 { get; set; }

    public int? PageCount { get; set; }

    // النص المستخرج بواسطة OCR للاحتفاظ به
    public string? OcrText { get; set; }

    public OcrStatus OcrStatus { get; set; }
        = OcrStatus.NotProcessed;

    // حذف منطقي لحماية الكتب
    public bool IsDeleted { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<BookMaterial> BookMaterials { get; set; }
        = new List<BookMaterial>();
}
