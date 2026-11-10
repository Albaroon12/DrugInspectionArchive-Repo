
using CommunityToolkit.Mvvm.ComponentModel;

namespace DrugInspectionArchive.ViewModels;

public partial class ReviewMaterialRow : ObservableObject
{
    [ObservableProperty]
    private string materialName = string.Empty;

    [ObservableProperty]
    private string batchNumber = string.Empty;

    [ObservableProperty]
    private int? pageNumber;

    [ObservableProperty]
    private float confidence;

    [ObservableProperty]
    private string sourceText = string.Empty;

    [ObservableProperty]
    private bool needsReview = true;

    // السجل مكتمل إذا كان له اسم ورقم تشغيلة (للأيقونة داخل الجدول)
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(MaterialName) &&
        !string.IsNullOrWhiteSpace(BatchNumber);

    partial void OnMaterialNameChanged(string value) =>
        OnPropertyChanged(nameof(IsComplete));

    partial void OnBatchNumberChanged(string value) =>
        OnPropertyChanged(nameof(IsComplete));

}
