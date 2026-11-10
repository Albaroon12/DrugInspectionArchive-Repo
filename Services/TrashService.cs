using DrugInspectionArchive.Data;
using Microsoft.EntityFrameworkCore;

namespace DrugInspectionArchive.Services;

public sealed class DeletedBookItem
{
    public int BookId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string? BookNumber { get; init; }
    public DateOnly? BookDate { get; init; }
    public int MaterialCount { get; init; }
    public DateTime? DeletedAtUtc { get; init; }
    public string PdfRelativePath { get; init; } = string.Empty;

    public string BookNumberText =>
        string.IsNullOrWhiteSpace(BookNumber) ? "—" : BookNumber;

    public string BookDateText => BookDate?.ToString("yyyy/MM/dd") ?? "—";

    public string DeletedAtText =>
        DeletedAtUtc?.ToLocalTime().ToString("yyyy/MM/dd HH:mm") ?? "—";
}

// سلة المحذوفات: نقل، استعادة، حذف نهائي
public sealed class TrashService
{
    private readonly ArchiveStorageService _storage = new();

    public async Task<List<DeletedBookItem>> GetTrashAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = ArchiveDbContextFactory.CreateContext();

        return await db.InspectionBooks
            .AsNoTracking()
            .Where(x => x.IsDeleted)
            .OrderByDescending(x => x.DeletedAtUtc)
            .Select(x => new DeletedBookItem
            {
                BookId = x.Id,
                CompanyName = x.Company.Name,
                BookNumber = x.BookNumber,
                BookDate = x.BookDate,
                MaterialCount = x.BookMaterials.Count,
                DeletedAtUtc = x.DeletedAtUtc,
                PdfRelativePath = x.PdfRelativePath
            })
            .ToListAsync(cancellationToken);
    }

    // نقل الكتاب وملفه إلى السلة
    public async Task MoveToTrashAsync(
        int bookId,
        CancellationToken cancellationToken = default)
    {
        await using var db = ArchiveDbContextFactory.CreateContext();

        var book = await db.InspectionBooks.FirstOrDefaultAsync(
                x => x.Id == bookId && !x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("كتاب الفحص غير موجود");

        string? trashPath = null;

        if (!string.IsNullOrWhiteSpace(book.PdfRelativePath) &&
            _storage.Exists(book.PdfRelativePath))
        {
            trashPath = _storage.MoveToTrash(book.PdfRelativePath, book.Id);
        }

        try
        {
            if (trashPath is not null)
                book.PdfRelativePath = trashPath;

            book.IsDeleted = true;
            book.DeletedAtUtc = DateTime.UtcNow;
            book.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (trashPath is not null)
            {
                try
                {
                    _storage.RestoreFromTrash(trashPath, book.CompanyId);
                }
                catch
                {
                }
            }

            throw;
        }
    }

    // استعادة الكتاب وملفه إلى الأرشيف
    public async Task RestoreAsync(
        int bookId,
        CancellationToken cancellationToken = default)
    {
        await using var db = ArchiveDbContextFactory.CreateContext();

        var book = await db.InspectionBooks.FirstOrDefaultAsync(
                x => x.Id == bookId && x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException(
                "الكتاب غير موجود في سلة المحذوفات");

        if (ArchiveStorageService.IsInTrash(book.PdfRelativePath) &&
            _storage.Exists(book.PdfRelativePath))
        {
            string restored = _storage.RestoreFromTrash(
                book.PdfRelativePath, book.CompanyId);

            book.PdfRelativePath =
                _storage.RenameStored(restored, book.BookNumber);
        }

        book.IsDeleted = false;
        book.DeletedAtUtc = null;
        book.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    // حذف نهائي: بيانات الكتاب وملفه (لا رجعة)
    public async Task PurgeAsync(
        int bookId,
        CancellationToken cancellationToken = default)
    {
        await using var db = ArchiveDbContextFactory.CreateContext();

        var book = await db.InspectionBooks
            .Include(x => x.BookMaterials)
            .FirstOrDefaultAsync(
                x => x.Id == bookId && x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException(
                "الكتاب غير موجود في سلة المحذوفات");

        string path = book.PdfRelativePath;

        db.BookMaterials.RemoveRange(book.BookMaterials);
        db.InspectionBooks.Remove(book);

        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(path))
            _storage.TryDeleteStoredPdf(path);
    }

    public async Task<int> EmptyTrashAsync(
        CancellationToken cancellationToken = default)
    {
        List<int> ids;

        await using (var db = ArchiveDbContextFactory.CreateContext())
        {
            ids = await db.InspectionBooks
                .Where(x => x.IsDeleted)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
        }

        foreach (int id in ids)
            await PurgeAsync(id, cancellationToken);

        return ids.Count;
    }

    // عند بدء البرنامج: ملفات المحذوفات القديمة إلى السلة، وتوحيد أسماء الباقي
    public async Task NormalizeArchiveAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = ArchiveDbContextFactory.CreateContext();

        var books = await db.InspectionBooks
            .Where(x => x.PdfRelativePath != "")
            .ToListAsync(cancellationToken);

        foreach (var book in books)
        {
            try
            {
                if (book.IsDeleted)
                {
                    if (!ArchiveStorageService.IsInTrash(book.PdfRelativePath) &&
                        _storage.Exists(book.PdfRelativePath))
                    {
                        book.PdfRelativePath =
                            _storage.MoveToTrash(book.PdfRelativePath, book.Id);
                    }
                }
                else if (_storage.Exists(book.PdfRelativePath))
                {
                    book.PdfRelativePath =
                        _storage.RenameStored(book.PdfRelativePath, book.BookNumber);
                }
            }
            catch
            {
                // ملف مشغول أو غير متاح: نتركه ونكمل
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}