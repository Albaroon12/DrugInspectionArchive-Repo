
using CommunityToolkit.Mvvm.ComponentModel;
using DrugInspectionArchive.Dtos;
using DrugInspectionArchive.Entities;
using DrugInspectionArchive.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using DrugInspectionArchive.Core;

namespace DrugInspectionArchive.ViewModels;

public partial class ReviewBookViewModel : ObservableObject
{
    private readonly CompanyService _companyService = new();

    private readonly InspectionBookService _bookService = new();
    private readonly ArchiveStorageService _storage = new();

    [ObservableProperty]
    private bool isSaving;
    public StagedPdfFile SourceFile { get; }

    public ObservableCollection<Company> Companies { get; } = new();

    public ObservableCollection<ReviewMaterialRow> Materials { get; } = new();

    [ObservableProperty]
    private Company? selectedCompany;

    [ObservableProperty]
    private ReviewMaterialRow? selectedMaterial;

    [ObservableProperty]
    private string bookNumber = string.Empty;

    // التاريخ الفعلي (يُحفظ منه)
    [ObservableProperty]
    private DateTime? bookDate;

    // نص التاريخ كما يكتبه المستخدم (2025/5/5 ...)
    [ObservableProperty]
    private string bookDateText = string.Empty;

    [ObservableProperty]
    private bool isBookDateInvalid;

    // اسم الشركة كما قُرئ من الكتاب (للعرض تحت القائمة)
    [ObservableProperty]
    private string extractedCompanyText = string.Empty;

    private bool _syncingDate;

    partial void OnBookDateTextChanged(string value)
    {
        if (_syncingDate)
            return;

        _syncingDate = true;

        try
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                BookDate = null;
                IsBookDateInvalid = false;
            }
            else if (TryParseFlexibleDate(value, out DateTime parsed))
            {
                BookDate = parsed;
                IsBookDateInvalid = false;
            }
            else
            {
                BookDate = null;
                IsBookDateInvalid = true;
            }
        }
        finally
        {
            _syncingDate = false;
        }
    }

    partial void OnBookDateChanged(DateTime? value)
    {
        if (_syncingDate)
            return;

        _syncingDate = true;

        try
        {
            BookDateText = value.HasValue
                ? value.Value.ToString(
                    "yyyy/MM/dd", CultureInfo.InvariantCulture)
                : string.Empty;

            IsBookDateInvalid = false;
        }
        finally
        {
            _syncingDate = false;
        }
    }

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    public int PageCount => SourceFile.PageCount;

    // عدد المواد المختلفة (المادة بعدة باتشات تُحسب مرة واحدة)
    public int MaterialCount => Materials
        .Select(x => TextNormalizer.NormalizeName(x.MaterialName))
        .Where(x => x.Length > 0)
        .Distinct()
        .Count();

    public string FileName => SourceFile.OriginalFileName;

    public ReviewBookViewModel(StagedPdfFile sourceFile)
    {
        SourceFile = sourceFile
            ?? throw new ArgumentNullException(nameof(sourceFile));

        Materials.CollectionChanged += (_, e) =>
{
    if (e.NewItems is not null)
    {
        foreach (ReviewMaterialRow row in e.NewItems)
        {
            row.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName ==
                    nameof(ReviewMaterialRow.MaterialName))
                    OnPropertyChanged(nameof(MaterialCount));
            };
        }
    }

    OnPropertyChanged(nameof(MaterialCount));
};
    }

    // تحميل بيانات المسودة
    public async Task LoadAsync(
        CancellationToken cancellationToken = default)
    {
        IsLoading = true;

        try
        {
            if (SourceFile.ParsedDraft is null)
            {
                throw new InvalidOperationException(
                    "لم يتم تحليل الكتاب بعد. نفذ OCR أولاً.");
            }

            var companies = await _companyService.GetAllAsync(
                includeInactive: false,
                cancellationToken);

            Companies.Clear();

            foreach (var company in companies)
                Companies.Add(company);

            var draft = SourceFile.ParsedDraft;

            BookNumber = draft.BookNumber.Value;

            BookDate = ParseDate(draft.BookDate.Value);

            ExtractedCompanyText = draft.CompanyName.Value;

            SelectedCompany = FindCompany(draft.CompanyName.Value);

            Materials.Clear();

            foreach (var item in MaterialSorter.Sort(
    draft.Materials,
    x => x.MaterialName,
    x => x.BatchNumber))
            {
                Materials.Add(new ReviewMaterialRow
                {
                    MaterialName = item.MaterialName,
                    BatchNumber = item.BatchNumber,
                    PageNumber = item.PageNumber,
                    Confidence = item.Confidence,
                    SourceText = item.SourceText,
                    NeedsReview = item.NeedsReview ||
                                  item.HasDuplicateEntry
                });
            }

            StatusMessage = SelectedCompany is null
                ? "اختر الشركة الصحيحة قبل اعتماد المراجعة."
                : $"تم تحميل {Materials.Count} سجل مادة للمراجعة.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // تحويل تاريخ OCR إلى تاريخ مناسب للتقويم
    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string[] formats =
        [
            "yyyy/MM/dd",
            "yyyy/M/d",
            "dd/MM/yyyy",
            "d/M/yyyy",
            "yyyy-MM-dd",
            "dd-MM-yyyy"
        ];

        if (DateOnly.TryParseExact(
            value.Trim(),
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date))
        {
            return date.ToDateTime(TimeOnly.MinValue);
        }

        return null;
    }

    // إضافة مادة جديدة
    public void AddMaterial()
    {
        Materials.Add(new ReviewMaterialRow
        {
            PageNumber = 1,
            NeedsReview = true
        });

        SelectedMaterial = Materials.Last();
    }

    // حذف المادة المحددة
    public void RemoveSelectedMaterial()
    {
        if (SelectedMaterial is null)
        {
            StatusMessage = "حدد سجل المادة المراد حذفه.";
            return;
        }

        Materials.Remove(SelectedMaterial);

        SelectedMaterial = null;

        StatusMessage = "تم حذف سجل المادة من المسودة.";
    }

    // إعادة تحميل الشركات
    public async Task ReloadCompaniesAsync(
        string? preferredName = null,
        CancellationToken cancellationToken = default)
    {
        var companies = await _companyService.GetAllAsync(
            cancellationToken: cancellationToken);

        Companies.Clear();

        foreach (var company in companies)
            Companies.Add(company);

        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            string normalized =
                TextNormalizer.NormalizeName(preferredName);

            SelectedCompany = Companies.FirstOrDefault(x =>
                x.NormalizedName == normalized);
        }
    }

    // التحقق من البيانات وتحديث المسودة
    public bool TryConfirmReview(out string error)
    {
        error = string.Empty;

        if (SelectedCompany is null)
        {
            error = "يجب اختيار الشركة المصنعة.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(BookNumber))
        {
            error = "رقم الكتاب مطلوب.";
            return false;
        }

        if (BookNumber.Trim().Length > 100)
        {
            error = "رقم الكتاب طويل جداً.";
            return false;
        }

        if (!BookDate.HasValue)
        {
            error = "يجب إدخال تاريخ الكتاب أو اختياره من التقويم.";
            return false;
        }

        if (Materials.Count == 0)
        {
            error = "يجب تسجيل مادة واحدة على الأقل.";
            return false;
        }

        foreach (var item in Materials)
        {
            if (string.IsNullOrWhiteSpace(item.MaterialName))
            {
                error = "يوجد سجل بدون اسم مادة.";
                return false;
            }

            if (item.MaterialName.Trim().Length > 250)
            {
                error = "اسم إحدى المواد طويل جداً.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.BatchNumber))
            {
                error = $"رقم Batch مطلوب للمادة: {item.MaterialName}";
                return false;
            }

            if (item.BatchNumber.Trim().Length > 120)
            {
                error = "أحد أرقام Batch طويل جداً.";
                return false;
            }

            if (item.PageNumber is null ||
                item.PageNumber < 1 ||
                item.PageNumber > PageCount)
            {
                error = $"رقم صفحة غير صحيح للمادة: {item.MaterialName}";
                return false;
            }
        }

        // السماح بتكرار المادة مع Batch مختلف
        var duplicateEntries = Materials
            .GroupBy(x => new
            {
                Material = TextNormalizer.NormalizeName(
                    x.MaterialName),

                Batch = TextNormalizer.NormalizeBatchNumber(
                    x.BatchNumber)
            })
            .Any(x => x.Count() > 1);

        if (duplicateEntries)
        {
            error =
                "توجد سجلات مكررة لنفس المادة ونفس Batch Number.";
            return false;
        }

        var draft = SourceFile.ParsedDraft!;

        draft.CompanyName.Value = SelectedCompany.Name;
        draft.BookNumber.Value = BookNumber.Trim();

        draft.BookDate.Value = DateOnly
            .FromDateTime(BookDate.Value)
            .ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

        draft.Materials = Materials.Select(item =>
            new ExtractedMaterialCandidate
            {
                MaterialName = item.MaterialName.Trim(),
                BatchNumber = item.BatchNumber.Trim(),
                PageNumber = item.PageNumber!.Value,
                Confidence = item.Confidence,
                SourceText = item.SourceText,
                NeedsReview = false,
                HasDuplicateEntry = false
            }).ToList();

        draft.Warnings.Clear();

        SourceFile.ProcessingStatus =
            "تمت مراجعة البيانات - بانتظار الحفظ";

        StatusMessage =
            "اكتملت المراجعة. البيانات جاهزة للاعتماد والحفظ.";

        return true;
    }

    // مراجعة + حفظ نهائي: نسخ PDF للأرشيف ثم حفظ الكتاب ومواده
        // حفظ كتاب جديد (اسم الملف: Inspection Drug#رقم الكتاب)
    public async Task<(bool Success, string Error)> SaveAsync()
    {
        if (IsSaving)
            return (false, "عملية الحفظ قيد التنفيذ");

        if (!TryConfirmReview(out string error))
            return (false, error);

        IsSaving = true;

        string? storedPath = null;

        try
        {
            int companyId = SelectedCompany!.Id;

            storedPath = await _storage.StorePdfAsync(
                SourceFile, companyId, BookNumber.Trim());

            var input = new ReviewedBookInput
            {
                CompanyId = companyId,
                BookNumber = BookNumber.Trim(),
                BookDate = DateOnly.FromDateTime(BookDate!.Value),
                OriginalFileName = SourceFile.OriginalFileName,
                PdfRelativePath = storedPath,
                FileSha256 = SourceFile.Sha256,
                PageCount = PageCount > 0 ? PageCount : null,
                OcrText = SourceFile.OcrText,
                Materials = Materials.Select(x => new ReviewedMaterialInput
                {
                    MaterialName = x.MaterialName.Trim(),
                    BatchNumber = x.BatchNumber.Trim(),
                    PageNumber = x.PageNumber
                }).ToList()
            };

            await _bookService.CreateReviewedBookAsync(input);

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            if (storedPath is not null)
                _storage.TryDeleteStoredPdf(storedPath);

            return (false, ex.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    // البحث عن كتاب مشابه داخل الشركة المختارة
    public Task<SimilarBookMatch?> FindSimilarAsync() =>
        _bookService.FindSimilarBookAsync(
            SelectedCompany!.Id,
            BookNumber.Trim(),
            DateOnly.FromDateTime(BookDate!.Value),
            Materials.Select(m => (m.MaterialName, m.BatchNumber)));

    // استبدال ملف كتاب موجود بالملف الجديد (تبقى بياناته كما هي)
    public async Task<(bool Success, string Error)> ReplaceFileAsync(
        ExistingBookSummary existing)
    {
        IsSaving = true;

        string? partial = null;

        try
        {
            partial = await _storage.PrepareReplacementAsync(
                SourceFile.TempFilePath, existing.PdfRelativePath);

            await _bookService.ReplaceFileAsync(
                existing.BookId,
                SourceFile.Sha256,
                SourceFile.OriginalFileName,
                PageCount > 0 ? PageCount : null,
                SourceFile.OcrText);

            _storage.CommitReplacement(partial, existing.PdfRelativePath);
            partial = null;

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        finally
        {
            if (partial is not null)
                _storage.DiscardReplacement(partial);

            IsSaving = false;
        }
    }

    // مطابقة الشركة المقروءة مع الشركات المسجلة (احتواء بدل التساوي التام)
    private Company? FindCompany(string? text)
    {
        string normalized = TextNormalizer.NormalizeName(text);

        if (normalized.Length < 2)
            return null;

        return Companies
            .Where(c => c.NormalizedName.Length >= 2 &&
                        (normalized.Contains(c.NormalizedName) ||
                         c.NormalizedName.Contains(normalized)))
            .OrderByDescending(c => c.NormalizedName.Length)
            .FirstOrDefault();
    }

    // قراءة تاريخ مرن: 2025/5/5 أو 5-5-2025 أو أرقام عربية
    private static bool TryParseFlexibleDate(string? text, out DateTime date)
    {
        date = default;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        var sb = new StringBuilder();

        foreach (char c in text.Trim())
        {
            sb.Append(c switch
            {
                >= '\u0660' and <= '\u0669' => (char)('0' + c - '\u0660'),
                >= '\u06F0' and <= '\u06F9' => (char)('0' + c - '\u06F0'),
                '-' or '.' or '\\' => '/',
                _ => c
            });
        }

        string[] formats = ["yyyy/M/d", "d/M/yyyy"];

        return DateTime.TryParseExact(
            sb.ToString(),
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    // إضافة سجل جديد لنفس المادة (لرقم تشغيلة آخر) تحت السجل المحدد
    public void AddBatchRowAfter(ReviewMaterialRow source)
    {
        var copy = new ReviewMaterialRow
        {
            MaterialName = source.MaterialName,
            PageNumber = source.PageNumber,
            NeedsReview = true
        };

        int index = Materials.IndexOf(source);

        Materials.Insert(
            index < 0 ? Materials.Count : index + 1,
            copy);

        SelectedMaterial = copy;

        StatusMessage =
            "أضفت سجلاً لنفس المادة. اكتب رقم التشغيلة الجديد.";
    }

    // حذف سجل محدد
    public void RemoveRow(ReviewMaterialRow row)
    {
        Materials.Remove(row);

        if (SelectedMaterial == row)
            SelectedMaterial = null;

        StatusMessage = "تم حذف السجل.";
    }
}
