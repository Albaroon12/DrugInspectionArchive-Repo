
namespace DrugInspectionArchive.Dtos;

public sealed class OcrDocumentResult
{
    public string FileName { get; init; } = string.Empty;

    public int PageCount { get; init; }

    public double AverageConfidence { get; init; }

    public string FullText { get; init; } = string.Empty;

    public List<OcrPageResult> Pages { get; init; } = [];

    public int TotalWords =>
        Pages.Sum(x => x.Words.Count);

    public int TotalLines =>
        Pages.Sum(x => x.Lines.Count);
}
