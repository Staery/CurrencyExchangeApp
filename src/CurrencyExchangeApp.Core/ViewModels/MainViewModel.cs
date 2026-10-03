using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CurrencyExchangeApp.Core.Abstractions;
using CurrencyExchangeApp.Core.Models;
using CurrencyExchangeApp.Core.Services;

namespace CurrencyExchangeApp.Core.ViewModels;

/// <summary>State and commands of the main window: currency and period selection, the rates table and the chart.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>Longest period that can be loaded at once (five years).</summary>
    public const int MaxPeriodDays = 5 * 365;

    private static readonly string[] DefaultCurrencies = ["USD", "EUR", "RUB", "CNY"];

    private readonly IExchangeRateClient _client;
    private readonly ExchangeRateService _service;
    private readonly IRateStore _store;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _time;
    private readonly List<RateRowViewModel> _allRows = [];
    private DateRange? _loadedRange;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeriodError), nameof(PeriodText))]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    private DateTime? _startDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PeriodError), nameof(PeriodText))]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    private DateTime? _endDate;

    [ObservableProperty]
    private string _currencySearch = string.Empty;

    [ObservableProperty]
    private string _tableSearch = string.Empty;

    [ObservableProperty]
    private string? _selectedChartCurrency;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChart))]
    private IReadOnlyList<ChartPoint> _chartPoints = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatistics))]
    private RateStatistics? _statistics;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand), nameof(SaveCommand), nameof(ExportCsvCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private int _progressMaximum = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _hasUnsavedChanges;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public MainViewModel(IExchangeRateClient client, IRateStore store, IDialogService dialogs, TimeProvider time)
    {
        _client = client;
        _service = new ExchangeRateService(client);
        _store = store;
        _dialogs = dialogs;
        _time = time;

        var today = Today;
        _endDate = today;
        _startDate = today.AddDays(-30);
    }

    public ObservableCollection<CurrencyOptionViewModel> Currencies { get; } = [];

    /// <summary>Currencies matching <see cref="CurrencySearch"/>.</summary>
    public ObservableCollection<CurrencyOptionViewModel> VisibleCurrencies { get; } = [];

    /// <summary>Rows matching <see cref="TableSearch"/>, newest first.</summary>
    public ObservableCollection<RateRowViewModel> Rows { get; } = [];

    /// <summary>Codes of the loaded currencies, for the chart selector.</summary>
    public ObservableCollection<string> LoadedCurrencies { get; } = [];

    public IReadOnlyList<PeriodPreset> PeriodPresets { get; } =
    [
        new("7 days", 7),
        new("30 days", 30),
        new("90 days", 90),
        new("1 year", 365),
    ];

    public int SelectedCount => Currencies.Count(currency => currency.IsSelected);

    public bool HasRows => _allRows.Count > 0;

    public bool HasStatistics => Statistics is not null;

    public bool HasChart => ChartPoints.Count > 0;

    public string StorageLocation => _store.Location;

    public string? PeriodError
    {
        get
        {
            if (StartDate is null || EndDate is null)
            {
                return "Choose both dates.";
            }

            var start = StartDate.Value.Date;
            var end = EndDate.Value.Date;

            if (start > end)
            {
                return "The start date must not be after the end date.";
            }

            if (end > Today)
            {
                return "Rates are not known for future dates.";
            }

            return new DateRange(start, end).Days > MaxPeriodDays ? "The period can be at most five years." : null;
        }
    }

    public string PeriodText => PeriodError is null && StartDate is { } start && EndDate is { } end
        ? $"{new DateRange(start.Date, end.Date).Days} days"
        : string.Empty;

    public string RowsSummary => Rows.Count == _allRows.Count
        ? $"{_allRows.Count:N0} rates"
        : $"{Rows.Count:N0} of {_allRows.Count:N0} rates";

    private DateTime Today => _time.GetLocalNow().Date;

    /// <summary>Restores saved rates and loads the list of currencies.</summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        RateSnapshot? snapshot = null;
        try
        {
            snapshot = await _store.LoadAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _dialogs.ShowError("Saved rates could not be read", ex.Message);
        }

        if (snapshot is { Records.Count: > 0 })
        {
            _loadedRange = new DateRange(snapshot.Start, snapshot.End);
            StartDate = snapshot.Start;
            EndDate = snapshot.End;
            SetRows(snapshot.Records);
            StatusMessage = $"Restored {snapshot.Records.Count:N0} rates saved on {snapshot.SavedAt.LocalDateTime:d MMM yyyy, HH:mm}.";
        }

        var selected = snapshot is { Records.Count: > 0 }
            ? new HashSet<string>(snapshot.Records.Select(record => record.Code))
            : new HashSet<string>(DefaultCurrencies);

        try
        {
            var currencies = await _client.GetDailyCurrenciesAsync();
            SetCurrencies(currencies, selected);
        }
        catch (ExchangeRateApiException ex)
        {
            // Offline: offer the currencies we have saved data for.
            var saved = snapshot?.Records
                .GroupBy(record => record.Code)
                .Select(group => group.First())
                .Select(record => new Currency(record.CurrencyId, record.Code, record.Name, record.Scale))
                .ToList() ?? [];

            SetCurrencies(saved, selected);
            StatusMessage = $"Working offline: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoad), IncludeCancelCommand = true)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var currencies = Currencies.Where(currency => currency.IsSelected).Select(currency => currency.Currency).ToList();
        var range = new DateRange(StartDate!.Value.Date, EndDate!.Value.Date);

        IsBusy = true;
        ProgressValue = 0;
        ProgressMaximum = currencies.Count;
        StatusMessage = $"Loading {currencies.Count} {Plural(currencies.Count, "currency", "currencies")} for {range}…";

        try
        {
            var progress = new Progress<int>(done => ProgressValue = done);
            var records = await _service.LoadAsync(currencies, range, progress, cancellationToken);

            _loadedRange = range;
            SetRows(records);
            await SaveCoreAsync();
            StatusMessage = $"Loaded {records.Count:N0} rates for {range}.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Loading cancelled.";
        }
        catch (ExchangeRateApiException ex)
        {
            _dialogs.ShowError("Rates could not be loaded", ex.Message);
            StatusMessage = "Loading failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanLoad() => !IsBusy && PeriodError is null && SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (await SaveCoreAsync())
        {
            StatusMessage = $"Saved to {_store.Location}";
        }
    }

    private bool CanSave() => HasUnsavedChanges && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportCsvAsync()
    {
        var suggested = _loadedRange is { } range ? $"rates_{range.Start:yyyyMMdd}-{range.End:yyyyMMdd}.csv" : "rates.csv";
        var path = _dialogs.PickCsvSavePath(suggested);
        if (path is null)
        {
            return;
        }

        try
        {
            await CsvExporter.ExportAsync(path, Rows.Select(row => row.ToRecord()));
            StatusMessage = $"Exported {Rows.Count:N0} rates to {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Export failed", ex.Message);
        }
    }

    private bool CanExport() => HasRows && !IsBusy;

    [RelayCommand]
    private void ApplyPreset(PeriodPreset? preset)
    {
        if (preset is null)
        {
            return;
        }

        var today = Today;
        EndDate = today;
        StartDate = today.AddDays(1 - preset.Days);
    }

    [RelayCommand]
    private void SelectAllCurrencies() => SetSelection(VisibleCurrencies, true);

    [RelayCommand]
    private void ClearCurrencies() => SetSelection(Currencies, false);

    partial void OnCurrencySearchChanged(string value) => RefreshVisibleCurrencies();

    partial void OnTableSearchChanged(string value) => RefreshRows();

    partial void OnSelectedChartCurrencyChanged(string? value) => RefreshChart();

    private void SetCurrencies(IReadOnlyList<Currency> currencies, ISet<string> selected)
    {
        foreach (var option in Currencies)
        {
            option.PropertyChanged -= OnCurrencyOptionChanged;
        }

        Currencies.Clear();
        foreach (var currency in currencies)
        {
            var option = new CurrencyOptionViewModel(currency) { IsSelected = selected.Contains(currency.Code) };
            option.PropertyChanged += OnCurrencyOptionChanged;
            Currencies.Add(option);
        }

        RefreshVisibleCurrencies();
        OnSelectionChanged();
    }

    private void OnCurrencyOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CurrencyOptionViewModel.IsSelected))
        {
            OnSelectionChanged();
        }
    }

    private void OnSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        LoadCommand.NotifyCanExecuteChanged();
    }

    private static void SetSelection(IEnumerable<CurrencyOptionViewModel> options, bool selected)
    {
        foreach (var option in options.ToList())
        {
            option.IsSelected = selected;
        }
    }

    private void RefreshVisibleCurrencies()
    {
        var term = CurrencySearch.Trim();
        VisibleCurrencies.Clear();

        foreach (var option in Currencies.Where(option =>
                     term.Length == 0 ||
                     option.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     option.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
        {
            VisibleCurrencies.Add(option);
        }
    }

    private void SetRows(IEnumerable<RateRecord> records)
    {
        foreach (var row in _allRows)
        {
            row.PropertyChanged -= OnRowChanged;
        }

        _allRows.Clear();

        foreach (var group in records.GroupBy(record => record.Code))
        {
            decimal? previous = null;
            foreach (var record in group.OrderBy(record => record.Date))
            {
                var row = new RateRowViewModel(record, previous);
                row.PropertyChanged += OnRowChanged;
                _allRows.Add(row);
                previous = record.Rate;
            }
        }

        var codes = _allRows.Select(row => row.Code).Distinct().OrderBy(code => code, StringComparer.Ordinal).ToList();
        var chartCurrency = SelectedChartCurrency is { } current && codes.Contains(current)
            ? current
            : codes.FirstOrDefault(code => code == "USD") ?? codes.FirstOrDefault();

        LoadedCurrencies.Clear();
        foreach (var code in codes)
        {
            LoadedCurrencies.Add(code);
        }

        HasUnsavedChanges = false;
        OnPropertyChanged(nameof(HasRows));
        ExportCsvCommand.NotifyCanExecuteChanged();
        RefreshRows();

        if (SelectedChartCurrency == chartCurrency)
        {
            RefreshChart();
        }
        else
        {
            SelectedChartCurrency = chartCurrency;
        }
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RateRowViewModel.Rate) && sender is RateRowViewModel row)
        {
            HasUnsavedChanges = true;
            StatusMessage = $"{row.Code} on {row.Date:dd.MM.yyyy} changed to {row.Rate}. Press Save to keep it.";

            if (row.Code == SelectedChartCurrency)
            {
                RefreshChart();
            }
        }
    }

    private void RefreshRows()
    {
        var term = TableSearch.Trim();

        Rows.Clear();
        foreach (var row in _allRows
                     .Where(row => term.Length == 0 ||
                                   row.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                   row.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                                   row.Date.ToString("dd.MM.yyyy").Contains(term, StringComparison.Ordinal))
                     .OrderByDescending(row => row.Date)
                     .ThenBy(row => row.Code, StringComparer.Ordinal))
        {
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(RowsSummary));
    }

    private void RefreshChart()
    {
        var points = _allRows
            .Where(row => row.Code == SelectedChartCurrency)
            .Select(row => new RatePoint(row.Date, row.Rate))
            .ToList();

        ChartPoints = points
            .OrderBy(point => point.Date)
            .Select(point => new ChartPoint(point.Date, (double)point.Rate))
            .ToList();
        Statistics = RateStatistics.Calculate(points);
    }

    private async Task<bool> SaveCoreAsync()
    {
        var snapshot = new RateSnapshot
        {
            SavedAt = _time.GetLocalNow(),
            Start = _loadedRange?.Start ?? default,
            End = _loadedRange?.End ?? default,
            Records = _allRows.Select(row => row.ToRecord()).ToList(),
        };

        try
        {
            await _store.SaveAsync(snapshot);
            HasUnsavedChanges = false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Rates could not be saved", ex.Message);
            return false;
        }
    }

    private static string Plural(int count, string one, string many) => count == 1 ? one : many;
}

public sealed record PeriodPreset(string Label, int Days);

/// <summary>A chart point; chart controls work with <see cref="DateTime"/> and <see cref="double"/>.</summary>
public sealed record ChartPoint(DateTime Date, double Rate);
