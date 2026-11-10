
namespace DrugInspectionArchive.Entities;

public sealed class BookMaterial
{
    public int Id { get; set; }

    public int InspectionBookId { get; set; }

    public InspectionBook InspectionBook { get; set; } = null!;

    public int MaterialId { get; set; }

    public Material Material { get; set; } = null!;

    // الاسم كما ظهر في كتاب الفحص
    public string? OriginalMaterialName { get; set; }

    // Batch واحد لكل مادة/تركيز داخل الكتاب
    public string BatchNumber { get; set; } = string.Empty;

    // نسخة موحدة للبحث
    public string NormalizedBatchNumber { get; set; }
        = string.Empty;

    // رقم الصفحة داخل PDF
    public int? PageNumber { get; set; }
}
