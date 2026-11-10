
using DrugInspectionArchive.Core;
using DrugInspectionArchive.Data;
using DrugInspectionArchive.Dtos;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Security.Cryptography;

namespace DrugInspectionArchive.Services;

public sealed class PdfImportService
{
    // استقبال ملف PDF وتجهيزه للمعالجة
    public async Task<StagedPdfFile> StageAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("مسار الملف غير صالح.");

        string fullPath = Path.GetFullPath(sourcePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException(
                "لم يتم العثور على الملف المحدد.", fullPath);

        if (!string.Equals(
            Path.GetExtension(fullPath),
            ".pdf",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "الملف المحدد ليس من نوع PDF.");
        }

        AppPaths.EnsureDirectories();

        string tempPath = Path.Combine(
            AppPaths.TempDirectory,
            $"{Guid.NewGuid():N}.pdf");

        try
        {
            // نسخ الملف إلى منطقة المعالجة المؤقتة
            await using (var source = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan))
            {
                if (source.Length == 0)
                    throw new InvalidOperationException(
                        "ملف PDF فارغ.");

                await using var destination = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);

                await source.CopyToAsync(
                    destination,
                    cancellationToken);
            }

            // التحقق من ترويسة PDF
            await using var stagedStream = new FileStream(
                tempPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous);

            byte[] header = new byte[
                (int)Math.Min(1024L, stagedStream.Length)];

            int bytesRead = await stagedStream.ReadAsync(
                header.AsMemory(),
                cancellationToken);

            bool validPdfHeader = false;

            for (int i = 0; i <= bytesRead - 5; i++)
            {
                if (header[i] == 0x25 &&
                    header[i + 1] == 0x50 &&
                    header[i + 2] == 0x44 &&
                    header[i + 3] == 0x46 &&
                    header[i + 4] == 0x2D)
                {
                    validPdfHeader = true;
                    break;
                }
            }

            if (!validPdfHeader)
                throw new InvalidOperationException(
                    "محتوى الملف لا يبدو ملف PDF صالحاً.");

            // حساب بصمة SHA-256
            stagedStream.Position = 0;

            byte[] hashBytes = await SHA256.HashDataAsync(
                stagedStream,
                cancellationToken);

            string hash = Convert.ToHexString(hashBytes);

            // التأكد من عدم أرشفة الملف سابقاً (بتوقيع SHA-256)
            var existing = await new InspectionBookService()
                .FindDuplicateAsync(
                    hash, 0, null, [], null, cancellationToken);

            if (existing is not null)
                throw new DuplicateBookException(existing);

            return new StagedPdfFile
            {
                TempFilePath = tempPath,
                OriginalFileName = Path.GetFileName(fullPath),
                Sha256 = hash,
                SizeBytes = new FileInfo(tempPath).Length
            };
        }
        catch
        {
            // تنظيف الملف المؤقت عند فشل المعالجة
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            throw;
        }
    }

    // التخلص من الملف المؤقت
    public Task DiscardAsync(
        StagedPdfFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        string tempRoot = Path.GetFullPath(
            AppPaths.TempDirectory);

        string fullPath = Path.GetFullPath(
            file.TempFilePath);

        string safeRoot = tempRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
            safeRoot,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "لا يمكن حذف ملف خارج مجلد المعالجة المؤقتة.");
        }

        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }
}
