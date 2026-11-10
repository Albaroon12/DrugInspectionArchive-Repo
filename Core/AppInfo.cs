using System.Reflection;

namespace DrugInspectionArchive.Core;

public sealed record ReleaseNote(string Version, string Date, string[] Items);

public static class AppInfo
{
    public const string Name = "أرشيف كتب فحص الأدوية";

    public static string Version
    {
        get
        {
            var v = Assembly.GetEntryAssembly()?.GetName().Version;
            return v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    public static readonly IReadOnlyList<ReleaseNote> Releases =
    [
        new ReleaseNote("1.0.0", "2026/11/10",
        [
            "قراءة ملفات PDF واستخراج المواد وأرقام التشغيلات تلقائياً",
            "مراجعة البيانات بجانب الملف الأصلي قبل الاعتماد",
            "كشف الملفات المكررة والكتب المتشابهة مع خيار الاستبدال",
            "سلة محذوفات مع استعادة وحذف نهائي",
            "تسمية الملفات تلقائياً بصيغة Inspection Drug#رقم الكتاب",
            "بحث سريع برقم التشغيلة أو المادة أو الشركة",
            "صفحة رئيسية جديدة وإعدادات ومعلومات البرنامج"
        ])
    ];
}