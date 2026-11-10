
using DrugInspectionArchive.Data;
using DrugInspectionArchive.Dtos;
using DrugInspectionArchive.Entities;
using DrugInspectionArchive.Enums;
using Microsoft.EntityFrameworkCore;

namespace DrugInspectionArchive.Services;

public sealed class ArchiveSearchService
{
    // =========================================
    // البحث برقم Batch Number
    // =========================================

    public async Task<List<ArchiveSearchResult>>
        SearchByBatchNumberAsync(
            string batchNumber,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
    {
        string normalized =
            TextNormalizer.NormalizeBatchNumber(batchNumber);

        if (string.IsNullOrWhiteSpace(normalized))
            return [];

        ValidatePagination(page, pageSize);

        await using var db =
            ArchiveDbContextFactory.CreateContext();

        return await db.BookMaterials
            .AsNoTracking()

            .Where(x =>
                x.NormalizedBatchNumber == normalized &&
                !x.InspectionBook.IsDeleted)

            .OrderByDescending(x => x.InspectionBook.BookDate)
            .ThenByDescending(x => x.InspectionBook.AddedAtUtc)

            .Skip((page - 1) * pageSize)
            .Take(pageSize)

            .Select(x => new ArchiveSearchResult
            {
                BookId = x.InspectionBookId,

                BookNumber = x.InspectionBook.BookNumber,

                CompanyName = x.InspectionBook.Company.Name,

                BookDate = x.InspectionBook.BookDate,

                AddedAtUtc = x.InspectionBook.AddedAtUtc,

                MaterialName =
                    x.OriginalMaterialName ??
                    x.Material.CanonicalName,

                BatchNumber = x.BatchNumber,

                PageNumber = x.PageNumber,

                PdfRelativePath =
                    x.InspectionBook.PdfRelativePath
            })

            .ToListAsync(cancellationToken);
    }

    // =========================================
    // البحث باسم المادة
    // =========================================

    public async Task<List<ArchiveSearchResult>>
        SearchByMaterialNameAsync(
            string materialName,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
    {
        string normalized =
            TextNormalizer.NormalizeName(materialName);

        if (normalized.Length < 2)
            return [];

        ValidatePagination(page, pageSize);

        await using var db =
            ArchiveDbContextFactory.CreateContext();

        return await db.BookMaterials
            .AsNoTracking()

            .Where(x =>
                x.Material.NormalizedName.Contains(normalized) &&
                !x.InspectionBook.IsDeleted)

            .OrderByDescending(x => x.InspectionBook.BookDate)
            .ThenByDescending(x => x.InspectionBook.AddedAtUtc)

            .Skip((page - 1) * pageSize)
            .Take(pageSize)

            .Select(x => new ArchiveSearchResult
            {
                BookId = x.InspectionBookId,

                BookNumber = x.InspectionBook.BookNumber,

                CompanyName = x.InspectionBook.Company.Name,

                BookDate = x.InspectionBook.BookDate,

                AddedAtUtc = x.InspectionBook.AddedAtUtc,

                MaterialName =
                    x.OriginalMaterialName ??
                    x.Material.CanonicalName,

                BatchNumber = x.BatchNumber,

                PageNumber = x.PageNumber,

                PdfRelativePath =
                    x.InspectionBook.PdfRelativePath
            })

            .ToListAsync(cancellationToken);
    }

    // =========================================
    // البحث عن الشركات بالاسم
    // =========================================

    public async Task<List<Company>> SearchCompaniesAsync(
        string companyName,
        CancellationToken cancellationToken = default)
    {
        string normalized =
            TextNormalizer.NormalizeName(companyName);

        if (normalized.Length < 2)
            return [];

        await using var db =
            ArchiveDbContextFactory.CreateContext();

        return await db.Companies
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                x.NormalizedName.Contains(normalized))
            .OrderBy(x => x.Name)
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    // =========================================
    // جلب كتب شركة محددة
    // =========================================

    public async Task<List<BookArchiveItem>>
        GetCompanyBooksAsync(
            int companyId,
            BookSortOrder sortOrder =
                BookSortOrder.BookDateNewest,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
    {
        ValidatePagination(page, pageSize);

        await using var db =
            ArchiveDbContextFactory.CreateContext();

        IQueryable<InspectionBook> query =
            db.InspectionBooks
                .AsNoTracking()
                .Where(x =>
                    x.CompanyId == companyId &&
                    !x.IsDeleted);

        // ترتيب الكتب
        query = sortOrder switch
        {
            BookSortOrder.BookDateNewest =>
                query.OrderByDescending(x => x.BookDate)
                     .ThenByDescending(x => x.AddedAtUtc),

            BookSortOrder.BookDateOldest =>
                query.OrderBy(x => x.BookDate)
                     .ThenBy(x => x.AddedAtUtc),

            BookSortOrder.AddedNewest =>
                query.OrderByDescending(x => x.AddedAtUtc),

            BookSortOrder.AddedOldest =>
                query.OrderBy(x => x.AddedAtUtc),

            BookSortOrder.BookNumberAscending =>
                query.OrderBy(x => x.NormalizedBookNumber),

            _ => query.OrderByDescending(x => x.BookDate)
        };

        return await query

            .Skip((page - 1) * pageSize)
            .Take(pageSize)

            .Select(x => new BookArchiveItem
            {
                Id = x.Id,

                BookNumber = x.BookNumber,

                BookDate = x.BookDate,

                AddedAtUtc = x.AddedAtUtc,

                MaterialCount = x.BookMaterials.Count,

                OcrStatus = x.OcrStatus,

                OriginalFileName = x.OriginalFileName,

                PdfRelativePath = x.PdfRelativePath
            })

            .ToListAsync(cancellationToken);


    }

    // =========================================
    // التحقق من إعدادات صفحات النتائج
    // =========================================

    // جلب الشركات النشطة مع عدد الكتب المؤرشفة لكل شركة
    public async Task<List<CompanyArchiveSummary>>
        GetCompanySummariesAsync(
            CancellationToken cancellationToken = default)
    {
        await using var db =
            ArchiveDbContextFactory.CreateContext();

        return await db.Companies
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new CompanyArchiveSummary
            {
                CompanyId = x.Id,
                CompanyName = x.Name,

                TotalBooks = x.Books.Count(
                    book => !book.IsDeleted)
            })
            .ToListAsync(cancellationToken);
    }

    // بحث موحد: رقم التشغيلة / المادة / الشركة / رقم الكتاب
    public async Task<List<ArchiveSearchResult>> SearchAsync(
        string query,
        SearchMode mode,
        int take = 300,
        CancellationToken cancellationToken = default)
    {
        string name = TextNormalizer.NormalizeName(query);
        string batch = TextNormalizer.NormalizeBatchNumber(query);

        if (name.Length < 2 || batch.Length < 2)
            return [];

        await using var db = ArchiveDbContextFactory.CreateContext();

        IQueryable<BookMaterial> q = db.BookMaterials
            .AsNoTracking()
            .Where(x => !x.InspectionBook.IsDeleted);

        q = mode switch
        {
            SearchMode.Batch =>
                q.Where(x => x.NormalizedBatchNumber.Contains(batch)),

            SearchMode.Material =>
                q.Where(x => x.Material.NormalizedName.Contains(name)),

            SearchMode.Company =>
                q.Where(x => x.InspectionBook.Company.NormalizedName
                    .Contains(name)),

            _ => q.Where(x =>
                x.NormalizedBatchNumber.Contains(batch) ||
                x.Material.NormalizedName.Contains(name) ||
                x.InspectionBook.Company.NormalizedName.Contains(name) ||
                (x.InspectionBook.NormalizedBookNumber != null &&
                 x.InspectionBook.NormalizedBookNumber.Contains(batch)))
        };

        return await q
            .OrderByDescending(x => x.NormalizedBatchNumber == batch)
            .ThenByDescending(x => x.InspectionBook.BookDate)
            .ThenByDescending(x => x.InspectionBook.AddedAtUtc)
            .Take(take)
            .Select(x => new ArchiveSearchResult
            {
                BookId = x.InspectionBookId,
                BookNumber = x.InspectionBook.BookNumber,
                CompanyName = x.InspectionBook.Company.Name,
                BookDate = x.InspectionBook.BookDate,
                AddedAtUtc = x.InspectionBook.AddedAtUtc,
                MaterialName =
                    x.OriginalMaterialName ?? x.Material.CanonicalName,
                BatchNumber = x.BatchNumber,
                PageNumber = x.PageNumber,
                PdfRelativePath = x.InspectionBook.PdfRelativePath
            })
            .ToListAsync(cancellationToken);
    }

    // الكتب (لكل الشركات أو لشركة محددة) الأحدث إضافة أولاً
    public async Task<List<BookArchiveItem>> GetBooksAsync(
        int? companyId,
        int take = 300,
        CancellationToken cancellationToken = default)
    {
        await using var db = ArchiveDbContextFactory.CreateContext();

        IQueryable<InspectionBook> query = db.InspectionBooks
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (companyId.HasValue)
            query = query.Where(x => x.CompanyId == companyId.Value);

        return await query
            .OrderByDescending(x => x.AddedAtUtc)
            .Take(take)
            .Select(x => new BookArchiveItem
            {
                Id = x.Id,
                CompanyId = x.CompanyId,
                CompanyName = x.Company.Name,
                BookNumber = x.BookNumber,
                BookDate = x.BookDate,
                AddedAtUtc = x.AddedAtUtc,
                MaterialCount = x.BookMaterials.Count,
                OcrStatus = x.OcrStatus,
                OriginalFileName = x.OriginalFileName,
                PdfRelativePath = x.PdfRelativePath
            })
            .ToListAsync(cancellationToken);
    }


    private static void ValidatePagination(
        int page,
        int pageSize)
    {
        if (page < 1)
            throw new ArgumentOutOfRangeException(
                nameof(page),
                "رقم الصفحة يجب أن يكون 1 أو أكبر.");

        if (pageSize < 1 || pageSize > 200)
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                "حجم الصفحة يجب أن يكون بين 1 و200.");
    }
}
