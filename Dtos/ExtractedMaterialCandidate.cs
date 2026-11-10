
namespace DrugInspectionArchive.Dtos;

public sealed class ExtractedMaterialCandidate
{
    public string MaterialName { get; set; } = string.Empty;

    public string BatchNumber { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public float Confidence { get; set; }

    public string SourceText { get; set; } = string.Empty;

    // هل يوجد تعارض مع قاعدة Batch الواحد؟
    public bool HasDuplicateEntry { get; set; }

    public bool NeedsReview { get; set; } = true;
}

