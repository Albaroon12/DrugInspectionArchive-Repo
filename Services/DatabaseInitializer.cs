
using DrugInspectionArchive.Core;
using DrugInspectionArchive.Data;
using Microsoft.EntityFrameworkCore;

namespace DrugInspectionArchive.Services;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        // التأكد من وجود مجلدات البرنامج
        AppPaths.EnsureDirectories();

        // إنشاء سياق قاعدة البيانات
        await using var db =
            ArchiveDbContextFactory.CreateContext();

        // تطبيق جميع التحديثات المسجلة في Migrations
        await db.Database.MigrateAsync(cancellationToken);
    }
}

