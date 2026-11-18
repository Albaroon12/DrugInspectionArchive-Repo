using DrugInspectionArchive.Core;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace DrugInspectionArchive.Services;

// أدوات المشاركة: واتساب سطح المكتب وبلوتوث ونسخ الملف
public sealed class ShareService
{
    private static string ShareDirectory =>
        Path.Combine(AppPaths.TempDirectory, "Share");

    // نسخة مؤقتة للمشاركة حتى لا يُقفل ملف الأرشيف الأصلي
    public string PrepareCopy(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException(
                "ملف PDF غير موجود في مجلد الأرشيف", sourcePath);

        Directory.CreateDirectory(ShareDirectory);

        // تنظيف النسخ القديمة
        foreach (var old in Directory.EnumerateFiles(ShareDirectory))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(old) < DateTime.UtcNow.AddDays(-1))
                    File.Delete(old);
            }
            catch
            {
            }
        }

        string fileName = Path.GetFileName(sourcePath);
        string target = Path.Combine(ShareDirectory, fileName);

        try
        {
            File.Copy(sourcePath, target, overwrite: true);
        }
        catch (IOException)
        {
            // النسخة السابقة مفتوحة في تطبيق آخر
            target = Path.Combine(
                ShareDirectory,
                $"{Path.GetFileNameWithoutExtension(fileName)} {DateTime.Now:HHmmss}.pdf");

            File.Copy(sourcePath, target, overwrite: true);
        }

        return target;
    }

    // نسخ الملف نفسه إلى الحافظة (يُلصق في أي تطبيق)
    public void CopyFileToClipboard(string path)
    {
        var files = new StringCollection { path };

        for (int attempt = 0; ; attempt++)
        {
            try
            {
                var data = new DataObject();
                data.SetFileDropList(files);
                Clipboard.SetDataObject(data, true);
                return;
            }
            catch (COMException) when (attempt < 4)
            {
                Thread.Sleep(120);
            }
        }
    }

    // فتح واتساب لسطح المكتب مع نص جاهز
    public void OpenWhatsApp(string text)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "whatsapp://send?text=" + Uri.EscapeDataString(text),
            UseShellExecute = true
        });
    }

    // معالج إرسال الملفات عبر بلوتوث في ويندوز
    public void SendBluetooth(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "fsquirt.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true
        });
    }
}