
namespace DrugInspectionArchive.Dtos;

public sealed class CompanyArchiveSummary
{
    public int CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;

    public int TotalBooks { get; set; }
}
