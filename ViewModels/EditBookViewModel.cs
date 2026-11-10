using CommunityToolkit.Mvvm.ComponentModel;
using DrugInspectionArchive.Core;
using DrugInspectionArchive.Dtos;
using DrugInspectionArchive.Entities;
using DrugInspectionArchive.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DrugInspectionArchive.ViewModels;

public partial class EditBookViewModel : ObservableObject
{
    private readonly CompanyService _companyService = new();
    private readonly InspectionBookService _bookService = new();


    private readonly ArchiveStorageService _storage = new();
    private string _originalNumber = string.Empty;

    public bool NumberChanged =>
        !string.Equals(BookNumber.Trim(), _originalNumber,
            StringComparison.OrdinalIgnoreCase);
    private bool _syncingDate;

    public int BookId { get; }

    public string PdfRelativePath { get; private set; } = string.Empty;

    public ObservableCollection<Company> Companies { get; } = new();

    public ObservableCollection<ReviewMaterialRow> Materials { get; } = new();

    [ObservableProperty] private Company? selectedCompany;
    [ObservableProperty] private ReviewMaterialRow? selectedMaterial;
    [ObservableProperty] private string bookNumber = string.Empty;
    [ObservableProperty] private DateTime? bookDate;
    [ObservableProperty] private string bookDateText = string.Empty;
    [ObservableProperty] private bool isBookDateInvalid;
    [ObservableProperty] private string fileName = string.Empty;
    [ObservableProperty] private bool isLoading;

    // عدد المواد المختلفة (المادة بعدة باتشات تُحسب مرة واحدة)
    public int MaterialCount => Materials
        .Select(x => TextNormalizer.NormalizeName(x.MaterialName))
        .Where(x => x.Length > 0)
        .Distinct()
        .Count();

    public EditBookViewModel(int bookId)
    {
        BookId = bookId;

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
            else if (DateInput.TryParseFlexible(value, out DateTime d))
            {
                BookDate = d;
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

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;

        try
        {
            var book = await _bookService.GetByIdAsync(
                    BookId, cancellationToken)
                ?? throw new KeyNotFoundException(
                    "كتاب الفحص غير موجود أو تم حذفه");

            var companies = await _companyService.GetAllAsync(
                includeInactive: false, cancellationToken);

            Companies.Clear();

            foreach (var company in companies)
                Companies.Add(company);

            if (Companies.All(x => x.Id != book.CompanyId))
                Companies.Add(book.Company);

            FileName = book.OriginalFileName;
            PdfRelativePath = book.PdfRelativePath;

            SelectedCompany = Companies.First(x => x.Id == book.CompanyId);
            BookNumber = book.BookNumber ?? string.Empty;
            _originalNumber = BookNumber.Trim();
            BookDate = book.BookDate?.ToDateTime(TimeOnly.MinValue);

            Materials.Clear();

            foreach (var m in MaterialSorter.Sort(
    book.BookMaterials.OrderBy(x => x.PageNumber).ToList(),
    x => x.OriginalMaterialName ?? x.Material.CanonicalName,
    x => x.BatchNumber))
            {
                Materials.Add(new ReviewMaterialRow
                {
                    MaterialName =
                        m.OriginalMaterialName ?? m.Material.CanonicalName,
                    BatchNumber = m.BatchNumber,
                    PageNumber = m.PageNumber,
                    NeedsReview = false
                });
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ReloadCompaniesAsync(string? preferredName)
    {
        var companies = await _companyService.GetAllAsync(
            includeInactive: false, default);

        Companies.Clear();

        foreach (var company in companies)
            Companies.Add(company);

        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            string normalized = TextNormalizer.NormalizeName(preferredName);

            SelectedCompany = Companies.FirstOrDefault(
                x => x.NormalizedName == normalized);
        }
    }

    public void AddMaterial()
    {
        var row = new ReviewMaterialRow { PageNumber = 1 };

        Materials.Add(row);
        SelectedMaterial = row;
    }

    // سجل جديد لنفس المادة (لرقم تشغيلة آخر)
    public void AddBatchRowAfter(ReviewMaterialRow source)
    {
        var copy = new ReviewMaterialRow
        {
            MaterialName = source.MaterialName,
            PageNumber = source.PageNumber,
            NeedsReview = false
        };

        int index = Materials.IndexOf(source);

        Materials.Insert(
            index < 0 ? Materials.Count : index + 1,
            copy);

        SelectedMaterial = copy;
    }

    public void RemoveRow(ReviewMaterialRow row)
    {
        Materials.Remove(row);

        if (SelectedMaterial == row)
            SelectedMaterial = null;
    }

    private bool TryValidate(out string error)
    {
        error = string.Empty;

        if (SelectedCompany is null)
            error = "اختر الشركة المصنعة";
        else if (string.IsNullOrWhiteSpace(BookNumber))
            error = "رقم الكتاب مطلوب";
        else if (!BookDate.HasValue)
            error = "أدخل تاريخ الكتاب بصيغة صحيحة";
        else if (Materials.Count == 0)
            error = "يجب تسجيل مادة واحدة على الأقل";
        else if (Materials.Any(x => string.IsNullOrWhiteSpace(x.MaterialName)))
            error = "يوجد سجل بدون اسم مادة";
        else
        {
            var missing = Materials.FirstOrDefault(
                x => string.IsNullOrWhiteSpace(x.BatchNumber));

            if (missing is not null)
                error = $"رقم التشغيلة مطلوب للمادة: {missing.MaterialName}";
        }

        return error.Length == 0;
    }

    public async Task<(bool Success, string Error)> SaveAsync()
    {
        if (!TryValidate(out string error))
            return (false, error);

        try
        {
            var input = new ReviewedBookInput
            {
                CompanyId = SelectedCompany!.Id,
                BookNumber = BookNumber.Trim(),
                BookDate = DateOnly.FromDateTime(BookDate!.Value),
                Materials = Materials.Select(x => new ReviewedMaterialInput
                {
                    MaterialName = x.MaterialName,
                    BatchNumber = x.BatchNumber,
                    PageNumber = x.PageNumber
                }).ToList()
            };

            await _bookService.UpdateBookAsync(BookId, input);

            // مواءمة اسم الملف مع رقم الكتاب الجديد
            if (NumberChanged)
            {
                try
                {
                    string renamed = _storage.RenameStored(
                        PdfRelativePath, BookNumber.Trim());

                    if (!string.Equals(renamed, PdfRelativePath,
                        StringComparison.Ordinal))
                    {
                        await _bookService.SetPdfPathAsync(BookId, renamed);
                        PdfRelativePath = renamed;
                    }
                }
                catch
                {
                    // يبقى الاسم القديم والبيانات محفوظة
                }

                _originalNumber = BookNumber.Trim();
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

        public Task DeleteAsync() => new TrashService().MoveToTrashAsync(BookId);
}