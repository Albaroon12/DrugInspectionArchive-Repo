
namespace DrugInspectionArchive.Dtos;

public sealed class ExtractedField
{
    public string Value { get; set; } = string.Empty;

    public string SourceText { get; set; } = string.Empty;

    public int? PageNumber { get; set; }

    public float Confidence { get; set; }

    public bool IsFound =>
        !string.IsNullOrWhiteSpace(Value);

    public bool NeedsReview =>
        !IsFound || Confidence < 85;
}

