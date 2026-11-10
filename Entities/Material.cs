
namespace DrugInspectionArchive.Entities;

public sealed class Material
{
    public int Id { get; set; }

    // الاسم المعتمد للمادة
    public string CanonicalName { get; set; } = string.Empty;

    // اسم موحد لأغراض البحث والمطابقة
    public string NormalizedName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public ICollection<BookMaterial> BookMaterials { get; set; }
        = new List<BookMaterial>();
}
