
namespace DrugInspectionArchive.Dtos;

public sealed class ArchiveSearchResult
{
    public int BookId { get; set; }

    public string? BookNumber { get; set; }

    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(BookNumber)
            ? "InspectionDrug #غير محدد"
            : $"InspectionDrug #{BookNumber}";

    public string CompanyName { get; set; } = string.Empty;

    public DateOnly? BookDate { get; set; }

    public DateTime AddedAtUtc { get; set; }

    public string MaterialName { get; set; } = string.Empty;

    public string BatchNumber { get; set; } = string.Empty;

    public int? PageNumber { get; set; }

    public string PdfRelativePath { get; set; } = string.Empty;

    public string BookNumberText =>
        string.IsNullOrWhiteSpace(BookNumber) ? "—" : BookNumber;

    public string BookDateText =>
        BookDate?.ToString("yyyy/MM/dd") ?? "—";

}
