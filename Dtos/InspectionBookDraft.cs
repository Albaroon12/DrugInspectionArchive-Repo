
namespace DrugInspectionArchive.Dtos;

public sealed class InspectionBookDraft
{
    public ExtractedField CompanyName { get; set; } = new();

    public ExtractedField BookNumber { get; set; } = new();

    public ExtractedField BookDate { get; set; } = new();

    public int PageCount { get; set; }

    public List<ExtractedMaterialCandidate> Materials { get; set; }
        = [];

    public List<string> Warnings { get; set; } = [];

    public int MaterialCount => Materials
        .Select(x => x.MaterialName)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int BatchCount => Materials.Count;

    public bool HasWarnings => Warnings.Count > 0;
}

