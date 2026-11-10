
namespace DrugInspectionArchive.Entities;

public sealed class Company
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // نسخة موحدة من الاسم لمنع التكرار
    public string NormalizedName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public ICollection<InspectionBook> Books { get; set; }
        = new List<InspectionBook>();
}