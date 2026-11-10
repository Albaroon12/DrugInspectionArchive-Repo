
using DrugInspectionArchive.Data;
using MaterialEntity = DrugInspectionArchive.Entities.Material;
using Microsoft.EntityFrameworkCore;

namespace DrugInspectionArchive.Services;

public sealed class MaterialService
{
    // عرض جميع المواد النشطة
    public async Task<List<MaterialEntity>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db =
            ArchiveDbContextFactory.CreateContext();

        return await db.Materials
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.CanonicalName)
            .ToListAsync(cancellationToken);
    }

    // البحث عن مادة باسمها
    public async Task<List<MaterialEntity>> SearchAsync(
        string searchText,
        CancellationToken cancellationToken = default)
    {
        string normalized =
            TextNormalizer.NormalizeName(searchText);

        if (normalized.Length < 2)
            return [];

        await using var db =
            ArchiveDbContextFactory.CreateContext();

        return await db.Materials
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.NormalizedName.Contains(normalized))
            .OrderBy(x => x.CanonicalName)
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    // جلب مادة حسب المعرف
    public async Task<MaterialEntity?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            ArchiveDbContextFactory.CreateContext();

        return await db.Materials
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    // إضافة تعريف مادة جديد
    public async Task<MaterialEntity> CreateAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم المادة مطلوب.");

        string displayName = name.Trim().Normalize();

        string normalized =
            TextNormalizer.NormalizeName(displayName);

        if (normalized.Length == 0)
            throw new ArgumentException("اسم المادة غير صالح.");

        if (displayName.Length > 250)
            throw new ArgumentException(
                "اسم المادة يجب ألا يتجاوز 250 حرفاً.");

        await using var db =
            ArchiveDbContextFactory.CreateContext();

        var existing = await db.Materials
            .FirstOrDefaultAsync(
                x => x.NormalizedName == normalized,
                cancellationToken);

        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"المادة مسجلة مسبقاً باسم: {existing.CanonicalName}");
        }

        var material = new MaterialEntity
        {
            CanonicalName = displayName,
            NormalizedName = normalized,
            IsActive = true
        };

        db.Materials.Add(material);

        await db.SaveChangesAsync(cancellationToken);

        return material;
    }

    // تعديل اسم المادة
    public async Task UpdateAsync(
        int id,
        string newName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("اسم المادة مطلوب.");

        string displayName = newName.Trim().Normalize();

        string normalized =
            TextNormalizer.NormalizeName(displayName);

        if (normalized.Length == 0 || displayName.Length > 250)
            throw new ArgumentException("اسم المادة غير صالح.");

        await using var db =
            ArchiveDbContextFactory.CreateContext();

        var material = await db.Materials
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (material is null)
            throw new KeyNotFoundException(
                "المادة غير موجودة.");

        bool duplicate = await db.Materials
            .AnyAsync(
                x => x.Id != id &&
                     x.NormalizedName == normalized,
                cancellationToken);

        if (duplicate)
        {
            throw new InvalidOperationException(
                "يوجد تعريف آخر للمادة بهذا الاسم.");
        }

        material.CanonicalName = displayName;
        material.NormalizedName = normalized;

        await db.SaveChangesAsync(cancellationToken);
    }
}
