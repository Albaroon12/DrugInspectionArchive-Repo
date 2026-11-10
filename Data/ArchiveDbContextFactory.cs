
using DrugInspectionArchive.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DrugInspectionArchive.Data;

public sealed class ArchiveDbContextFactory
    : IDesignTimeDbContextFactory<ArchiveDbContext>
{
    // إنشاء قاعدة البيانات للاستخدام أثناء تشغيل البرنامج
    public static ArchiveDbContext CreateContext()
    {
        AppPaths.EnsureDirectories();

        var options =
            new DbContextOptionsBuilder<ArchiveDbContext>()
                .UseSqlite(
                    $"Data Source={AppPaths.DatabaseFilePath}")
                .Options;

        return new ArchiveDbContext(options);
    }

    // تستخدمها أداة dotnet ef
    public ArchiveDbContext CreateDbContext(string[] args)
    {
        return CreateContext();
    }
}
