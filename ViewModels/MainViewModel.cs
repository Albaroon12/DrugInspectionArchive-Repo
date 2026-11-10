using CommunityToolkit.Mvvm.ComponentModel;
using DrugInspectionArchive.Dtos;
using DrugInspectionArchive.Enums;
using DrugInspectionArchive.Services;
using System.Collections.ObjectModel;

namespace DrugInspectionArchive.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ArchiveSearchService _searchService = new();

    private CancellationTokenSource? _searchCts;
    private bool _loadingCompanies;

    public ObservableCollection<CompanyArchiveSummary> Companies { get; } = new();

    public ObservableCollection<BookArchiveItem> Books { get; } = new();

    public ObservableCollection<ArchiveSearchResult> Results { get; } = new();

    [ObservableProperty] private CompanyArchiveSummary? selectedCompany;
    [ObservableProperty] private string query = string.Empty;
    [ObservableProperty] private SearchMode mode = SearchMode.All;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string resultsSummary = string.Empty;
    [ObservableProperty] private string archiveSummaryText = string.Empty;

    public bool IsSearching => !string.IsNullOrWhiteSpace(Query);

    public bool BooksEmpty => !IsSearching && !IsBusy && Books.Count == 0;

    public bool ResultsEmpty => IsSearching && !IsBusy && Results.Count == 0;

    public string ContentTitle =>
        IsSearching
            ? "نتائج البحث"
            : SelectedCompany is { CompanyId: > 0 } c
                ? c.CompanyName
                : "آخر الكتب المضافة";

    public string ContentSummary =>
        IsSearching
            ? ResultsSummary
            : Books.Count == 1 ? "كتاب واحد" : $"{Books.Count} كتاب";

    partial void OnIsBusyChanged(bool value) => RefreshState();

    partial void OnResultsSummaryChanged(string value) => RefreshState();

    partial void OnQueryChanged(string value)
    {
        RefreshState();
        _ = DebouncedSearchAsync();
    }

    partial void OnModeChanged(SearchMode value)
    {
        if (IsSearching)
            _ = DebouncedSearchAsync();
    }

    partial void OnSelectedCompanyChanged(CompanyArchiveSummary? value)
    {
        if (_loadingCompanies)
            return;

        // اختيار شركة يعيدنا لوضع التصفح
        if (IsSearching)
            Query = string.Empty;

        _ = LoadBooksAsync();
        RefreshState();
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(BooksEmpty));
        OnPropertyChanged(nameof(ResultsEmpty));
        OnPropertyChanged(nameof(ContentTitle));
        OnPropertyChanged(nameof(ContentSummary));
    }

    // تحميل الشركات والكتب
    public async Task LoadAsync()
    {
        IsBusy = true;

        try
        {
            int keepId = SelectedCompany?.CompanyId ?? 0;

            var summaries = await _searchService.GetCompanySummariesAsync();

            int total = summaries.Sum(x => x.TotalBooks);

            var all = new CompanyArchiveSummary
            {
                CompanyId = 0,
                CompanyName = "كل الشركات",
                TotalBooks = total
            };

            _loadingCompanies = true;

            Companies.Clear();
            Companies.Add(all);

            foreach (var summary in summaries)
                Companies.Add(summary);

            SelectedCompany =
                Companies.FirstOrDefault(x => x.CompanyId == keepId) ?? all;

            _loadingCompanies = false;

            ArchiveSummaryText = $"{summaries.Count} شركة  ·  {total} كتاب";

            await LoadBooksAsync();

            if (IsSearching)
                await RunSearchAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            ArchiveSummaryText =
                "تعذر تحميل الأرشيف: " + ex.Message.TrimEnd('.', ' ');
        }
        finally
        {
            _loadingCompanies = false;
            IsBusy = false;
        }
    }

    private async Task LoadBooksAsync()
    {
        try
        {
            int? companyId = SelectedCompany is { CompanyId: > 0 } c
                ? c.CompanyId
                : null;

            var books = await _searchService.GetBooksAsync(companyId, 300);

            Books.Clear();

            foreach (var book in books)
                Books.Add(book);
        }
        catch (Exception ex)
        {
            ArchiveSummaryText =
                "تعذر تحميل الكتب: " + ex.Message.TrimEnd('.', ' ');
        }

        RefreshState();
    }

    // بحث بعد توقف الكتابة لحظة
    private async Task DebouncedSearchAsync()
    {
        _searchCts?.Cancel();

        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(350, cts.Token);
            await RunSearchAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    // بحث فوري (زر Enter / قارئ الباركود)
    public async Task SearchNowAsync()
    {
        _searchCts?.Cancel();

        var cts = _searchCts = new CancellationTokenSource();

        try
        {
            await RunSearchAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunSearchAsync(CancellationToken cancellationToken)
    {
        if (!IsSearching)
        {
            Results.Clear();
            ResultsSummary = string.Empty;
            RefreshState();
            return;
        }

        if (Query.Trim().Length < 2)
        {
            Results.Clear();
            ResultsSummary = "اكتب حرفين على الأقل";
            RefreshState();
            return;
        }

        try
        {
            var list = await _searchService.SearchAsync(
                Query, Mode, 300, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            Results.Clear();

            foreach (var item in list)
                Results.Add(item);

            ResultsSummary = list.Count switch
            {
                0 => "لا توجد نتائج مطابقة",
                1 => "نتيجة واحدة",
                _ => $"{list.Count} نتيجة"
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            ResultsSummary = "تعذر البحث: " + ex.Message.TrimEnd('.', ' ');
        }

        RefreshState();
    }
}