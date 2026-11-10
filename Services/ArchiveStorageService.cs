using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using DrugInspectionArchive.Core;
using DrugInspectionArchive.Dtos;

namespace DrugInspectionArchive.Services;

// إدارة ملفات PDF الدائمة داخل مجلد الأرشيف
public sealed class ArchiveStorageService
{
    private const string TrashFolder = "Trash";

    // اسم الملف المعتمد: Inspection Drug#رقم الكتاب
    public static string BuildFileBaseName(string? bookNumber)
    {
        string number = bookNumber ?? string.Empty;

        foreach (char c in Path.GetInvalidFileNameChars())
            number = number.Replace(c, '-');

        number = Regex.Replace(number, @"\s+", " ").Trim(' ', '.', '-');

        if (number.Length > 80)
            number = number[..80];

        if (number.Length == 0)
            number = "NoNumber";

        return "Inspection Drug#" + number;
    }

    // مسار غير مستعمل: الاسم، ثم الاسم (2)، (3) ...
    private static string UniquePath(string folder, string baseName)
    {
        string path = Path.Combine(folder, baseName + ".pdf");

        for (int i = 2; File.Exists(path) || File.Exists(path + ".partial"); i++)
            path = Path.Combine(folder, $"{baseName} ({i}).pdf");

        return path;
    }

    // نسخ الملف المؤقت إلى مجلد الشركة وإرجاع المسار النسبي
    public async Task<string> StorePdfAsync(
        StagedPdfFile file,
        int companyId,
        string? bookNumber = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!File.Exists(file.TempFilePath))
            throw new FileNotFoundException(
                "النسخة المؤقتة من الملف غير موجودة", file.TempFilePath);

        AppPaths.EnsureDirectories();

        string folder = Path.Combine(
            AppPaths.CompanyDocumentsDirectory, companyId.ToString());

        Directory.CreateDirectory(folder);

        string baseName = bookNumber is null
            ? file.Sha256.ToUpperInvariant()
            : BuildFileBaseName(bookNumber);

        string destination = UniquePath(folder, baseName);
        string partial = destination + ".partial";

        try
        {
            await using (var source = File.OpenRead(file.TempFilePath))
            await using (var target = File.Create(partial))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            File.Move(partial, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(partial))
                File.Delete(partial);
        }

        return $"Companies/{companyId}/{Path.GetFileName(destination)}";
    }

    // ===== السلة =====

    public string MoveToTrash(string relativePath, int bookId)
    {
        string source = ResolveFullPath(relativePath);
        string fileName = Path.GetFileName(source);

        string folder = Path.Combine(
            AppPaths.DocumentsDirectory, TrashFolder, bookId.ToString());

        Directory.CreateDirectory(folder);

        File.Move(source, Path.Combine(folder, fileName), overwrite: true);

        return $"{TrashFolder}/{bookId}/{fileName}";
    }

    public string RestoreFromTrash(string trashRelativePath, int companyId)
    {
        string source = ResolveFullPath(trashRelativePath);

        string folder = Path.Combine(
            AppPaths.CompanyDocumentsDirectory, companyId.ToString());

        Directory.CreateDirectory(folder);

        string destination = UniquePath(
            folder, Path.GetFileNameWithoutExtension(source));

        File.Move(source, destination);

        TryRemoveEmptyFolder(Path.GetDirectoryName(source));

        return $"Companies/{companyId}/{Path.GetFileName(destination)}";
    }

    public static bool IsInTrash(string relativePath) =>
        relativePath.Replace('\\', '/')
            .StartsWith(TrashFolder + "/", StringComparison.OrdinalIgnoreCase);

    // ===== تغيير الاسم ليوافق رقم الكتاب =====

    public string RenameStored(string relativePath, string? bookNumber)
    {
        string source = ResolveFullPath(relativePath);

        if (!File.Exists(source))
            return relativePath;

        string baseName = BuildFileBaseName(bookNumber);
        string current = Path.GetFileNameWithoutExtension(source);

        if (current.Equals(baseName, StringComparison.OrdinalIgnoreCase) ||
            Regex.IsMatch(
                current,
                "^" + Regex.Escape(baseName) + @" \(\d+\)$",
                RegexOptions.IgnoreCase))
        {
            return relativePath;
        }

        string destination = UniquePath(Path.GetDirectoryName(source)!, baseName);

        File.Move(source, destination);

        string rel = relativePath.Replace('\\', '/');

        return rel[..rel.LastIndexOf('/')] + "/" + Path.GetFileName(destination);
    }

    // ===== استبدال محتوى ملف موجود (يبقى الاسم والمسار) =====

    public async Task<string> PrepareReplacementAsync(
        string sourceTempPath,
        string existingRelativePath,
        CancellationToken cancellationToken = default)
    {
        string destination = ResolveFullPath(existingRelativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        string partial = destination + ".new";

        await using (var source = File.OpenRead(sourceTempPath))
        await using (var target = File.Create(partial))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        return partial;
    }

    public void CommitReplacement(string partialPath, string existingRelativePath) =>
        File.Move(partialPath, ResolveFullPath(existingRelativePath), overwrite: true);

    public void DiscardReplacement(string partialPath)
    {
        try
        {
            if (File.Exists(partialPath))
                File.Delete(partialPath);
        }
        catch
        {
        }
    }

    // ===== حذف وفتح =====

    public void TryDeleteStoredPdf(string relativePath)
    {
        try
        {
            string fullPath = ResolveFullPath(relativePath);

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            if (IsInTrash(relativePath))
                TryRemoveEmptyFolder(Path.GetDirectoryName(fullPath));
        }
        catch
        {
        }
    }

    private static void TryRemoveEmptyFolder(string? folder)
    {
        try
        {
            if (folder is not null &&
                Directory.Exists(folder) &&
                !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
        catch
        {
        }
    }

    public string ResolveFullPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("مسار الملف فارغ");

        string root = Path.GetFullPath(AppPaths.DocumentsDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        string fullPath = Path.GetFullPath(
            Path.Combine(
                AppPaths.DocumentsDirectory,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("مسار الملف خارج مجلد الأرشيف");

        return fullPath;
    }

    public bool Exists(string relativePath)
    {
        try
        {
            return File.Exists(ResolveFullPath(relativePath));
        }
        catch
        {
            return false;
        }
    }

    public void OpenPdf(string relativePath)
    {
        string fullPath = ResolveFullPath(relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException(
                "ملف PDF غير موجود في مجلد الأرشيف", fullPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true
        });
    }

    public void ShowInFolder(string relativePath)
    {
        string fullPath = ResolveFullPath(relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException(
                "ملف PDF غير موجود في مجلد الأرشيف", fullPath);

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{fullPath}\"",
            UseShellExecute = true
        });
    }
}