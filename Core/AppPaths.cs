
using System.IO;

namespace DrugInspectionArchive.Core;

public static class AppPaths
{
    public static string RootDirectory =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "DrugInspectionArchive");

    public static string DatabaseDirectory =>
        Path.Combine(RootDirectory, "Database");

    public static string DatabaseFilePath =>
        Path.Combine(DatabaseDirectory, "Archive.db");

    public static string DocumentsDirectory =>
        Path.Combine(RootDirectory, "Documents");

    public static string CompanyDocumentsDirectory =>
        Path.Combine(DocumentsDirectory, "Companies");

    public static string BackupsDirectory =>
        Path.Combine(RootDirectory, "Backups");

    public static string TempDirectory =>
        Path.Combine(RootDirectory, "Temp");

    public static string LogsDirectory =>
        Path.Combine(RootDirectory, "Logs");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DatabaseDirectory);
        Directory.CreateDirectory(DocumentsDirectory);
        Directory.CreateDirectory(CompanyDocumentsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }

    
    // مكان ملفات OCR داخل مجلد تشغيل البرنامج
    public static string OcrRuntimeDirectory =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Ocr");

    // مجلد ملفات اللغات
    public static string TessDataDirectory =>
        Path.Combine(
            OcrRuntimeDirectory,
            "tessdata");

}
