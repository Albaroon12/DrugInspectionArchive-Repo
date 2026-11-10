using DrugInspectionArchive.Enums;
using System.IO;
using System.Text.Json;

namespace DrugInspectionArchive.Core;

// إعدادات بسيطة تُحفظ في settings.json داخل مجلد البرنامج
public static class AppSettings
{
    private sealed class Data
    {
        public SearchMode DefaultMode { get; set; } = SearchMode.All;
        public BookSort DefaultSort { get; set; } = BookSort.Added;
    }

    private static readonly string FilePath =
        Path.Combine(AppPaths.RootDirectory, "settings.json");

    private static readonly Data Current = Load();

    public static SearchMode DefaultMode
    {
        get => Current.DefaultMode;
        set { Current.DefaultMode = value; Save(); }
    }

    public static BookSort DefaultSort
    {
        get => Current.DefaultSort;
        set { Current.DefaultSort = value; Save(); }
    }

    private static Data Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Data>(
                    File.ReadAllText(FilePath)) ?? new Data();
        }
        catch
        {
        }

        return new Data();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.RootDirectory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current));
        }
        catch
        {
        }
    }
}