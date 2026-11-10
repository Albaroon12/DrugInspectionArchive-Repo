
namespace DrugInspectionArchive.Dtos;

public sealed class OcrLineResult
{
    public string Text { get; init; } = string.Empty;

    public float Confidence { get; init; }

    public int? X1 { get; init; }
    public int? Y1 { get; init; }
    public int? X2 { get; init; }
    public int? Y2 { get; init; }

    public List<OcrWordResult> Words { get; init; } = [];

    public int CenterY =>
        Y1.HasValue && Y2.HasValue
            ? (Y1.Value + Y2.Value) / 2
            : 0;
}
