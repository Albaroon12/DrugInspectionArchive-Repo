
namespace DrugInspectionArchive.Dtos;

public sealed class OcrPageResult
{
    public int PageNumber { get; init; }

    public string Text { get; init; } = string.Empty;

    public float Confidence { get; init; }

    // أبعاد صورة الصفحة
    public int ImageWidth { get; init; }

    public int ImageHeight { get; init; }

    // الأسطر التي تعرف عليها OCR
    public List<OcrLineResult> Lines { get; init; } = [];

    // جميع الكلمات وإحداثياتها
    public List<OcrWordResult> Words { get; init; } = [];

    // كلمات عمود المنتجات بعد إعادة قراءته بالإنجليزية فقط
    public List<OcrWordResult> ProductWords { get; set; } = [];
}
