
namespace DrugInspectionArchive.Dtos;

public sealed class ReviewedBookInput
{
    public int CompanyId { get; set; }

    public string? BookNumber { get; set; }

    public DateOnly? BookDate { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string PdfRelativePath { get; set; } = string.Empty;

    public string? FileSha256 { get; set; }

    public int? PageCount { get; set; }

    public string? OcrText { get; set; }

    public List<ReviewedMaterialInput> Materials { get; set; } = [];
}

public sealed class ReviewedMaterialInput
{
    // الاسم الكامل مع الجرعة
    public string MaterialName { get; set; } = string.Empty;

    public string BatchNumber { get; set; } = string.Empty;

    public int? PageNumber { get; set; }
}
