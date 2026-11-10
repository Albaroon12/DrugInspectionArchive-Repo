
using DrugInspectionArchive.Data;
using DrugInspectionArchive.Entities;
using Microsoft.EntityFrameworkCore;

namespace DrugInspectionArchive.Services;

public sealed class CompanyService
{
    // عرض الشركات
    public async Task<List<Company>> GetAllAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            ArchiveDbContextFactory.CreateContext();

        IQueryable<Company> query =
            db.Companies.AsNoTracking();

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        return await query
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    // البحث عن شركة برقمها
    public async Task<Company?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            ArchiveDbContextFactory.CreateContext();

        return await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    // البحث عن شركة باسمها بعد توحيد النص
    public async Task<Company?> FindByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        string normalized =
            TextNormalizer.NormalizeName(name);

        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        await using var db =
            ArchiveDbContextFactory.CreateContext();

        return await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.NormalizedName == normalized,
                cancellationToken);
    }

    // إضافة شركة جديدة
    public async Task<Company> CreateAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "اسم الشركة مطلوب.");

        string displayName =
            name.Trim().Normalize();

        string normalized =
            TextNormalizer.NormalizeName(displayName);

        if (normalized.Length == 0)
            throw new ArgumentException(
                "اسم الشركة غير صالح.");

        if (displayName.Length > 200)
            throw new ArgumentException(
                "اسم الشركة يجب ألا يتجاوز 200 حرف.");

        await using var db =
            ArchiveDbContextFactory.CreateContext();

        bool exists = await db.Companies
            .AnyAsync(
                x => x.NormalizedName == normalized,
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException(
                "هذه الشركة مسجلة مسبقاً. ابحث عنها واخترها بدلاً من إضافتها مرة أخرى.");
        }

        var company = new Company
        {
            Name = displayName,
            NormalizedName = normalized,
            IsActive = true
        };

        db.Companies.Add(company);

        await db.SaveChangesAsync(cancellationToken);

        return company;
    }

    // تعديل اسم شركة
    public async Task UpdateAsync(
        int id,
        string newName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException(
                "اسم الشركة مطلوب.");

        string displayName =
            newName.Trim().Normalize();

        string normalized =
            TextNormalizer.NormalizeName(displayName);

        if (displayName.Length > 200 ||
            string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException(
                "اسم الشركة غير صالح أو يتجاوز الحد المسموح.");
        }

        await using var db =
            ArchiveDbContextFactory.CreateContext();

        var company = await db.Companies
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (company is null)
            throw new KeyNotFoundException(
                "الشركة غير موجودة.");

        bool duplicate = await db.Companies
            .AnyAsync(
                x => x.Id != id &&
                     x.NormalizedName == normalized,
                cancellationToken);

        if (duplicate)
            throw new InvalidOperationException(
                "يوجد شركة أخرى مسجلة بهذا الاسم.");

        company.Name = displayName;
        company.NormalizedName = normalized;

        await db.SaveChangesAsync(cancellationToken);
    }

    // تعطيل الشركة بدلاً من حذفها
    public async Task DeactivateAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            ArchiveDbContextFactory.CreateContext();

        var company = await db.Companies
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (company is null)
            throw new KeyNotFoundException(
                "الشركة غير موجودة.");

        company.IsActive = false;

        await db.SaveChangesAsync(cancellationToken);
    }

    // إعادة تفعيل الشركة
    public async Task ActivateAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            ArchiveDbContextFactory.CreateContext();

        var company = await db.Companies
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (company is null)
            throw new KeyNotFoundException(
                "الشركة غير موجودة.");

        company.IsActive = true;

        await db.SaveChangesAsync(cancellationToken);
    }
}
