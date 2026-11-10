
namespace DrugInspectionArchive.Dtos;

public sealed class OcrWordResult
{
    public string Text { get; init; } = string.Empty;

    public float Confidence { get; init; }

    // إحداثيات الكلمة داخل الصورة
    public int? X1 { get; init; }
    public int? Y1 { get; init; }
    public int? X2 { get; init; }
    public int? Y2 { get; init; }

    public int Width =>
        X1.HasValue && X2.HasValue
            ? X2.Value - X1.Value
            : 0;

    public int Height =>
        Y1.HasValue && Y2.HasValue
            ? Y2.Value - Y1.Value
            : 0;

    public int CenterX =>
        X1.HasValue && X2.HasValue
            ? (X1.Value + X2.Value) / 2
            : 0;

    public int CenterY =>
        Y1.HasValue && Y2.HasValue
            ? (Y1.Value + Y2.Value) / 2
            : 0;
}
