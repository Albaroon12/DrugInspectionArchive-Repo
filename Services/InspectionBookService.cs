
using System.IO;
using DrugInspectionArchive.Data;
using DrugInspectionArchive.Dtos;
using DrugInspectionArchive.Entities;
using DrugInspectionArchive.Enums;
using Microsoft.EntityFrameworkCore;
using MaterialEntity = DrugInspectionArchive.Entities.Material;

namespace DrugInspectionArchive.Services;

public sealed class InspectionBookService
{
    // إضافة كتاب بعد مراجعة المستخدم للبيانات
    public async Task<int> CreateReviewedBookAsync(
        ReviewedBookInput input,
        CancellationToken cancellationToken = default)
    {
        ValidateInput(input);

        await using var db =
            ArchiveDbContextFactory.CreateContext();

        // عملية واحدة لجميع بيانات الكتاب ومواده
        await using var transaction =
            await db.Database.BeginTransactionAsync(
                cancellationToken);

        // التأكد من وجود الشركة وأنها فعالة
        var company = await db.Companies
            .FirstOrDefaultAsync(
                x => x.Id == input.CompanyId,
                cancellationToken);

        if (company is null || !company.IsActive)
        {
            throw new InvalidOperationException(
                "الشركة غير موجودة أو غير فعالة.");
        }

        string? fileHash = string.IsNullOrWhiteSpace(input.FileSha256)
            ? null
            : input.FileSha256.Trim().ToUpperInvariant();

        // منع التكرار: نفس الملف أو نفس المحتوى
        var existing = await FindDuplicateAsync(
            fileHash,
            input.CompanyId,
            input.BookNumber,
            input.Materials.Select(m => (m.MaterialName, m.BatchNumber)),
                        null,
            cancellationToken,
            bookDate: input.BookDate);

        if (existing is not null)
            throw new DuplicateBookException(existing);

        // تجهيز بيانات المواد والتحقق منها

        var preparedMaterials = input.Materials
            .Select(item =>
            {
                string name = item.MaterialName.Trim().Normalize();

                string normalizedName =
                    TextNormalizer.NormalizeName(name);

                string batch =
                    item.BatchNumber.Trim().Normalize();

                string normalizedBatch =
                    TextNormalizer.NormalizeBatchNumber(batch);

                return new
                {
                    Name = name,
                    NormalizedName = normalizedName,
                    Batch = batch,
                    NormalizedBatch = normalizedBatch,
                    item.PageNumber
                };
            })
            .ToList();

        // تحميل المواد الموجودة مسبقاً دفعة واحدة
        var normalizedNames = preparedMaterials
            .Select(x => x.NormalizedName)
            .Distinct()
            .ToList();

        var existingMaterials = await db.Materials
            .Where(x => normalizedNames.Contains(x.NormalizedName))
            .ToListAsync(cancellationToken);

        var materialDictionary = existingMaterials
            .ToDictionary(x => x.NormalizedName);

        // إنشاء تعريفات المواد الجديدة فقط
        foreach (var item in preparedMaterials)
        {
            if (materialDictionary.ContainsKey(item.NormalizedName))
                continue;

            var material = new MaterialEntity
            {
                CanonicalName = item.Name,
                NormalizedName = item.NormalizedName,
                IsActive = true
            };

            materialDictionary.Add(
                item.NormalizedName,
                material);

            db.Materials.Add(material);
        }

        // إنشاء الكتاب
        var book = new InspectionBook
        {
            CompanyId = input.CompanyId,

            BookNumber = string.IsNullOrWhiteSpace(input.BookNumber)
                ? null
                : input.BookNumber.Trim(),

            NormalizedBookNumber =
                string.IsNullOrWhiteSpace(input.BookNumber)
                    ? null
                    : TextNormalizer.NormalizeBatchNumber(
                        input.BookNumber),

            BookDate = input.BookDate,

            OriginalFileName = input.OriginalFileName.Trim(),

            PdfRelativePath = input.PdfRelativePath
                .Replace('\\', '/'),

            FileSha256 = fileHash,

            PageCount = input.PageCount,

            OcrText = input.OcrText,

            OcrStatus = OcrStatus.Confirmed,

            AddedAtUtc = DateTime.UtcNow
        };

        db.InspectionBooks.Add(book);

        // ربط المواد بالكتاب
        foreach (var item in preparedMaterials)
        {
            var material =
                materialDictionary[item.NormalizedName];

            db.BookMaterials.Add(new BookMaterial
            {
                InspectionBook = book,

                Material = material,

                OriginalMaterialName = item.Name,

                BatchNumber = item.Batch,

                NormalizedBatchNumber = item.NormalizedBatch,

                PageNumber = item.PageNumber
            });
        }

        // حفظ الكتاب وجميع مواده معاً
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return book.Id;
    }

    // جلب كتاب مع الشركة وجميع المواد التابعة له
    public async Task<InspectionBook?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            ArchiveDbContextFactory.CreateContext();

        return await db.InspectionBooks
            .AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
            .Include(x => x.Company)
            .Include(x => x.BookMaterials)
                .ThenInclude(x => x.Material)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // حذف منطقي للكتاب
    public async Task SoftDeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            ArchiveDbContextFactory.CreateContext();

        var book = await db.InspectionBooks
            .FirstOrDefaultAsync(
                x => x.Id == id && !x.IsDeleted,
                cancellationToken);

        if (book is null)
            throw new KeyNotFoundException("كتاب الفحص غير موجود.");

        book.IsDeleted = true;
        book.DeletedAtUtc = DateTime.UtcNow;
        book.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    // استعادة كتاب محذوف
    public async Task RestoreAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var db =
            ArchiveDbContextFactory.CreateContext();

        var book = await db.InspectionBooks
            .FirstOrDefaultAsync(
                x => x.Id == id && x.IsDeleted,
                cancellationToken);

        if (book is null)
            throw new KeyNotFoundException(
                "الكتاب غير موجود أو غير محذوف.");

        book.IsDeleted = false;
        book.DeletedAtUtc = null;
        book.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    // البحث عن كتاب مكرر: بنفس الملف (التوقيع) أو بنفس المحتوى
    public async Task<ExistingBookSummary?> FindDuplicateAsync(
        string? fileSha256,
        int companyId,
        string? bookNumber,
        IEnumerable<(string Name, string Batch)> materials,
                int? excludeBookId = null,
        CancellationToken cancellationToken = default,
        DateOnly? bookDate = null)
    {
        await using var db = ArchiveDbContextFactory.CreateContext();

        // 1) نفس الملف (يشمل الكتب المحذوفة)
        if (!string.IsNullOrWhiteSpace(fileSha256))
        {
            string hash = fileSha256.Trim().ToUpperInvariant();

            var sameFile = await db.InspectionBooks
                .AsNoTracking()
                .Where(x => x.FileSha256 == hash &&
                            (excludeBookId == null || x.Id != excludeBookId))
                .OrderBy(x => x.IsDeleted)
                .Select(x => new ExistingBookSummary
                {
                    BookId = x.Id,
                    CompanyName = x.Company.Name,
                    BookNumber = x.BookNumber,
                    BookDate = x.BookDate,
                    MaterialCount = x.BookMaterials.Count,
                    IsDeleted = x.IsDeleted,
                    OriginalFileName = x.OriginalFileName,
                    SameFile = true
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (sameFile is not null)
                return sameFile;
        }

        // 2) ملف مختلف بنفس الشركة ورقم الكتاب والمواد والباتشات
        string normalizedNumber =
            TextNormalizer.NormalizeBatchNumber(bookNumber);

        if (normalizedNumber.Length == 0)
            return null;

        var wanted = materials
            .Select(m =>
                TextNormalizer.NormalizeName(m.Name) + "|" +
                TextNormalizer.NormalizeBatchNumber(m.Batch))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        if (wanted.Count == 0)
            return null;

        var candidates = await db.InspectionBooks
            .AsNoTracking()
            .Where(x => !x.IsDeleted &&
                        x.CompanyId == companyId &&
                        x.NormalizedBookNumber == normalizedNumber &&
                        (excludeBookId == null || x.Id != excludeBookId))
            .Select(x => new
            {
                x.Id,
                CompanyName = x.Company.Name,
                x.BookNumber,
                x.BookDate,
                x.OriginalFileName,
                Materials = x.BookMaterials
                    .Select(m => new
                    {
                        Name = m.Material.NormalizedName,
                        Batch = m.NormalizedBatchNumber
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        foreach (var c in candidates)
        {
            var have = c.Materials
                .Select(m => m.Name + "|" + m.Batch)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToList();

            if (c.BookDate == bookDate && have.SequenceEqual(wanted))
            {
                return new ExistingBookSummary
                {
                    BookId = c.Id,
                    CompanyName = c.CompanyName,
                    BookNumber = c.BookNumber,
                    BookDate = c.BookDate,
                    MaterialCount = c.Materials.Count,
                    IsDeleted = false,
                    OriginalFileName = c.OriginalFileName,
                    SameFile = false
                };
            }
        }

        return null;
    }

    // تعديل بيانات كتاب موجود (الشركة، الرقم، التاريخ، المواد)
    public async Task UpdateBookAsync(
        int bookId,
        ReviewedBookInput input,
        CancellationToken cancellationToken = default)
    {
        await using var db = ArchiveDbContextFactory.CreateContext();

        var book = await db.InspectionBooks
            .Include(x => x.BookMaterials)
            .FirstOrDefaultAsync(
                x => x.Id == bookId && !x.IsDeleted,
                cancellationToken);

        if (book is null)
            throw new KeyNotFoundException("كتاب الفحص غير موجود");

        // بيانات الملف تبقى من السجل الحالي، ثم نتحقق بنفس قواعد الإضافة
        input.OriginalFileName = book.OriginalFileName;
        input.PdfRelativePath = book.PdfRelativePath;
        input.PageCount = book.PageCount;
        input.FileSha256 = book.FileSha256;

        ValidateInput(input);

        var company = await db.Companies.FirstOrDefaultAsync(
            x => x.Id == input.CompanyId,
            cancellationToken);

        if (company is null || !company.IsActive)
            throw new InvalidOperationException(
                "الشركة غير موجودة أو غير فعالة");

        // منع تحويل الكتاب إلى نسخة مطابقة لكتاب آخر
        var existing = await FindDuplicateAsync(
            null,
            input.CompanyId,
            input.BookNumber,
            input.Materials.Select(m => (m.MaterialName, m.BatchNumber)),
                        bookId,
            cancellationToken,
            bookDate: input.BookDate);

        if (existing is not null)
            throw new DuplicateBookException(existing);

        var prepared = input.Materials
            .Select(m =>
            {
                string name = m.MaterialName.Trim().Normalize();
                string batch = m.BatchNumber.Trim().Normalize();

                return new
                {
                    Name = name,
                    NormalizedName = TextNormalizer.NormalizeName(name),
                    Batch = batch,
                    NormalizedBatch =
                        TextNormalizer.NormalizeBatchNumber(batch),
                    m.PageNumber
                };
            })
            .ToList();

        var names = prepared
            .Select(x => x.NormalizedName)
            .Distinct()
            .ToList();

        var materialMap = (await db.Materials
                .Where(x => names.Contains(x.NormalizedName))
                .ToListAsync(cancellationToken))
            .ToDictionary(x => x.NormalizedName);

        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        // حذف المواد القديمة أولاً (حفظ مستقل لتفادي تعارض الفهارس)
        db.BookMaterials.RemoveRange(book.BookMaterials);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var item in prepared)
        {
            if (materialMap.ContainsKey(item.NormalizedName))
                continue;

            var material = new MaterialEntity
            {
                CanonicalName = item.Name,
                NormalizedName = item.NormalizedName,
                IsActive = true
            };

            materialMap.Add(item.NormalizedName, material);
            db.Materials.Add(material);
        }

        book.CompanyId = input.CompanyId;

        book.BookNumber = string.IsNullOrWhiteSpace(input.BookNumber)
            ? null
            : input.BookNumber.Trim();

        book.NormalizedBookNumber =
            string.IsNullOrWhiteSpace(input.BookNumber)
                ? null
                : TextNormalizer.NormalizeBatchNumber(input.BookNumber);

        book.BookDate = input.BookDate;
        book.UpdatedAtUtc = DateTime.UtcNow;

        foreach (var item in prepared)
        {
            db.BookMaterials.Add(new BookMaterial
            {
                InspectionBook = book,
                Material = materialMap[item.NormalizedName],
                OriginalMaterialName = item.Name,
                BatchNumber = item.Batch,
                NormalizedBatchNumber = item.NormalizedBatch,
                PageNumber = item.PageNumber
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // مقارنة كتاب جديد بكتب الشركة: أولاً برقم الكتاب، ثم التاريخ والمواد والباتشات
    public async Task<SimilarBookMatch?> FindSimilarBookAsync(
        int companyId,
        string? bookNumber,
        DateOnly? bookDate,
        IEnumerable<(string Name, string Batch)> materials,
        CancellationToken cancellationToken = default)
    {
        string number = TextNormalizer.NormalizeBatchNumber(bookNumber);

        if (number.Length == 0)
            return null;

        var wanted = materials
            .Select(m => new
            {
                m.Name,
                m.Batch,
                Key = TextNormalizer.NormalizeName(m.Name) + "|" +
                      TextNormalizer.NormalizeBatchNumber(m.Batch)
            })
            .ToList();

        var wantedKeys = wanted.Select(x => x.Key).ToHashSet();

        await using var db = ArchiveDbContextFactory.CreateContext();

        var candidates = await db.InspectionBooks
            .AsNoTracking()
            .Where(x => !x.IsDeleted &&
                        x.CompanyId == companyId &&
                        x.NormalizedBookNumber == number)
            .Select(x => new
            {
                x.Id,
                x.CompanyId,
                CompanyName = x.Company.Name,
                x.BookNumber,
                x.BookDate,
                x.OriginalFileName,
                x.PdfRelativePath,
                Materials = x.BookMaterials
                    .Select(m => new
                    {
                        Name = m.OriginalMaterialName ?? m.Material.CanonicalName,
                        NName = m.Material.NormalizedName,
                        Batch = m.BatchNumber,
                        NBatch = m.NormalizedBatchNumber
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        static string Fmt(DateOnly? d) =>
            d?.ToString("yyyy/MM/dd") ?? "غير محدد";

        SimilarBookMatch? best = null;

        foreach (var c in candidates)
        {
            var differences = new List<string>();

            if (c.BookDate != bookDate)
                differences.Add(
                    $"تاريخ الكتاب: {Fmt(c.BookDate)} في الأرشيف، و{Fmt(bookDate)} في الكتاب الجديد");

            var oldKeys = c.Materials
                .Select(m => m.NName + "|" + m.NBatch)
                .ToHashSet();

            foreach (var w in wanted.Where(x => !oldKeys.Contains(x.Key)))
                differences.Add($"غير موجود في الأرشيف: {w.Name} | {w.Batch}");

            foreach (var m in c.Materials.Where(
                m => !wantedKeys.Contains(m.NName + "|" + m.NBatch)))
                differences.Add($"غير موجود في الكتاب الجديد: {m.Name} | {m.Batch}");

            var match = new SimilarBookMatch
            {
                Differences = differences,
                Existing = new ExistingBookSummary
                {
                    BookId = c.Id,
                    CompanyId = c.CompanyId,
                    CompanyName = c.CompanyName,
                    BookNumber = c.BookNumber,
                    BookDate = c.BookDate,
                    MaterialCount = c.Materials.Count,
                    OriginalFileName = c.OriginalFileName,
                    PdfRelativePath = c.PdfRelativePath
                }
            };

            if (best is null ||
                match.Differences.Count < best.Differences.Count)
                best = match;
        }

        return best;
    }

    // استبدال ملف الكتاب: تبقى كل البيانات، وتتغير بيانات الملف فقط
    public async Task ReplaceFileAsync(
        int bookId,
        string newSha256,
        string originalFileName,
        int? pageCount,
        string? ocrText,
        CancellationToken cancellationToken = default)
    {
        await using var db = ArchiveDbContextFactory.CreateContext();

        var book = await db.InspectionBooks.FirstOrDefaultAsync(
                x => x.Id == bookId && !x.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("كتاب الفحص غير موجود");

        book.FileSha256 = newSha256.Trim().ToUpperInvariant();
        book.OriginalFileName = originalFileName.Trim();
        book.PageCount = pageCount;
        book.OcrText = ocrText;
        book.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    // تحديث مسار الملف بعد إعادة التسمية
    public async Task SetPdfPathAsync(
        int bookId,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        await using var db = ArchiveDbContextFactory.CreateContext();

        var book = await db.InspectionBooks.FirstOrDefaultAsync(
            x => x.Id == bookId, cancellationToken);

        if (book is null)
            return;

        book.PdfRelativePath = relativePath.Replace('\\', '/');

        await db.SaveChangesAsync(cancellationToken);
    }

    // التحقق من البيانات قبل الحفظ

    private static void ValidateInput(ReviewedBookInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.CompanyId <= 0)
            throw new ArgumentException("يجب تحديد الشركة.");

        if (string.IsNullOrWhiteSpace(input.OriginalFileName))
            throw new ArgumentException("اسم ملف PDF مطلوب.");

        if (input.OriginalFileName.Length > 255)
            throw new ArgumentException("اسم الملف طويل جداً.");

        if (string.IsNullOrWhiteSpace(input.PdfRelativePath))
            throw new ArgumentException("مسار PDF مطلوب.");

        string[] pathParts = input.PdfRelativePath.Split(
            new[] { '/', '\\' },
            StringSplitOptions.RemoveEmptyEntries);

        if (Path.IsPathRooted(input.PdfRelativePath) ||
            pathParts.Any(x => x == ".."))
        {
            throw new ArgumentException(
                "يجب أن يكون مسار PDF نسبياً وآمناً داخل الأرشيف.");
        }

        if (input.PageCount is <= 0)
            throw new ArgumentException(
                "عدد صفحات الكتاب غير صالح.");

        if (!string.IsNullOrWhiteSpace(input.BookNumber) &&
            input.BookNumber.Trim().Length > 100)
        {
            throw new ArgumentException(
                "رقم الكتاب يتجاوز الحد المسموح.");
        }

        if (!string.IsNullOrWhiteSpace(input.FileSha256))
        {
            string hash = input.FileSha256.Trim();

            if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                throw new ArgumentException(
                    "بصمة الملف SHA-256 غير صالحة.");
        }

        if (input.Materials is null || input.Materials.Count == 0)
            throw new ArgumentException(
                "يجب أن يحتوي كتاب الفحص على مادة واحدة على الأقل.");

        foreach (var item in input.Materials)
        {
            if (string.IsNullOrWhiteSpace(item.MaterialName))
                throw new ArgumentException(
                    "يوجد سجل مادة بدون اسم.");

            if (item.MaterialName.Trim().Length > 250)
                throw new ArgumentException(
                    "اسم إحدى المواد طويل جداً.");

            if (string.IsNullOrWhiteSpace(item.BatchNumber))
                throw new ArgumentException(
                    $"يجب إدخال Batch Number للمادة: {item.MaterialName}");

            if (item.BatchNumber.Trim().Length > 120)
                throw new ArgumentException(
                    "رقم إحدى التشغيلات طويل جداً.");

            if (item.PageNumber is <= 0)
                throw new ArgumentException(
                    "رقم صفحة إحدى المواد غير صالح.");

            if (input.PageCount.HasValue &&
                item.PageNumber > input.PageCount)
            {
                throw new ArgumentException(
                    "رقم صفحة إحدى المواد أكبر من عدد صفحات الكتاب.");
            }
        }

        // منع تكرار المادة نفسها بنفس التركيز داخل الكتاب
        // منع تكرار نفس المادة ونفس Batch فقط
        var duplicateEntries = input.Materials
            .GroupBy(x => new
            {
                Material = TextNormalizer.NormalizeName(
                    x.MaterialName),

                Batch = TextNormalizer.NormalizeBatchNumber(
                    x.BatchNumber)
            })
            .Any(group => group.Count() > 1);

        if (duplicateEntries)
        {
            throw new ArgumentException(
                "تم تسجيل نفس المادة ونفس Batch Number أكثر من مرة داخل الكتاب.");
        }
    }

}
