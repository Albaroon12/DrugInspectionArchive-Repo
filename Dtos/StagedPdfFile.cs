
using CommunityToolkit.Mvvm.ComponentModel;
using DrugInspectionArchive.Dtos;
namespace DrugInspectionArchive.Dtos;

public sealed partial class StagedPdfFile : ObservableObject
{
    public string TempFilePath { get; init; } = string.Empty;

    public string OriginalFileName { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    [ObservableProperty]
    private string processingStatus = "بانتظار القراءة";

    [ObservableProperty]
    private string ocrText = string.Empty;

    [ObservableProperty]
    private int pageCount;

    [ObservableProperty]
    private double? meanConfidence;

    [ObservableProperty]
    private bool isOcrCompleted;

    [ObservableProperty]
    private InspectionBookDraft? parsedDraft;

    // نتيجة OCR الكاملة مع إحداثيات الكلمات
    [ObservableProperty]
    private OcrDocumentResult? ocrResult;

    public string SizeText =>
        SizeBytes >= 1024 * 1024
            ? $"{SizeBytes / 1024d / 1024d:0.##} MB"
            : $"{SizeBytes / 1024d:0.#} KB";

   public string ConfidenceText =>
    MeanConfidence.HasValue
        ? $"مستوى الثقة: {MeanConfidence.Value:0.0}%"
        : "لم تتم القراءة بعد";

    partial void OnMeanConfidenceChanged(double? value)
    {
        OnPropertyChanged(nameof(ConfidenceText));
    }
}
