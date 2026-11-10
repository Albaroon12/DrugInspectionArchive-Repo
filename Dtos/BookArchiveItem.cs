
using DrugInspectionArchive.Enums;

namespace DrugInspectionArchive.Dtos;

public sealed class BookArchiveItem
{
    public int Id { get; set; }

    public string? BookNumber { get; set; }

    public int CompanyId { get; set; }

    public string CompanyName { get; set; } = string.Empty;


    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(BookNumber)
            ? "InspectionDrug #غير محدد"
            : $"InspectionDrug #{BookNumber}";

    public DateOnly? BookDate { get; set; }

    public DateTime AddedAtUtc { get; set; }

    public int MaterialCount { get; set; }

    public OcrStatus OcrStatus { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string PdfRelativePath { get; set; } = string.Empty;
    public string BookDateText =>
        BookDate?.ToString("yyyy/MM/dd") ?? "غير محدد";

    public string AddedDateText =>
        AddedAtUtc.ToLocalTime().ToString("yyyy/MM/dd");

    public string MaterialCountText =>
        MaterialCount == 1
            ? "مادة واحدة"
            : $"{MaterialCount} مواد";

    public string BookNumberText =>
        string.IsNullOrWhiteSpace(BookNumber) ? "—" : BookNumber;
}
