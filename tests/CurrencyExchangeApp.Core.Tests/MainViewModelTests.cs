using CurrencyExchangeApp.Core.Models;
using CurrencyExchangeApp.Core.Services;
using CurrencyExchangeApp.Core.ViewModels;

namespace CurrencyExchangeApp.Core.Tests;

public class MainViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeClient _client = new();
    private readonly MemoryStore _store = new();
    private readonly FakeDialogs _dialogs = new();

    private MainViewModel Create() => new(_client, _store, _dialogs, new FixedTime(Now));

    private static DateTime D(int month, int day) => new(2026, month, day);

    [Fact]
    public async Task Initialize_SelectsDefaultCurrencies()
    {
        var vm = Create();

        await vm.InitializeCommand.ExecuteAsync(null);

        Assert.Equal(5, vm.Currencies.Count);
        Assert.Equal(["USD", "EUR", "RUB", "CNY"], vm.Currencies.Where(c => c.IsSelected).Select(c => c.Code));
        Assert.Equal(4, vm.SelectedCount);
        Assert.False(vm.HasRows);
        Assert.Equal(D(10, 3), vm.EndDate);
        Assert.Null(vm.PeriodError);
    }

    [Fact]
    public async Task Initialize_RestoresSavedRatesAndSelection()
    {
        _store.Saved = new RateSnapshot
        {
            Start = new DateTime(2026, 9, 1),
            End = new DateTime(2026, 9, 2),
            Records =
            [
                new RateRecord(new DateTime(2026, 9, 1), 426, "PLN", "Злотых", 10, 8.1m),
                new RateRecord(new DateTime(2026, 9, 2), 426, "PLN", "Злотых", 10, 8.2m),
            ],
        };
        var vm = Create();

        await vm.InitializeCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal(["PLN"], vm.Currencies.Where(c => c.IsSelected).Select(c => c.Code));
        Assert.Equal("PLN", vm.SelectedChartCurrency);
        Assert.Equal(D(9, 1), vm.StartDate);
        Assert.NotNull(vm.Statistics);
    }

    [Fact]
    public async Task Initialize_Offline_OffersSavedCurrencies()
    {
        _client.CurrenciesError = new ExchangeRateApiException("offline");
        _store.Saved = new RateSnapshot
        {
            Start = new DateTime(2026, 9, 1),
            End = new DateTime(2026, 9, 1),
            Records = [new RateRecord(new DateTime(2026, 9, 1), 431, "USD", "Доллар США", 1, 2.9m)],
        };
        var vm = Create();

        await vm.InitializeCommand.ExecuteAsync(null);

        Assert.Equal("USD", Assert.Single(vm.Currencies).Code);
        Assert.Contains("offline", vm.StatusMessage);
        Assert.Empty(_dialogs.Errors);
    }

    [Theory]
    [InlineData(9, 10, 9, 1, "start date")]
    [InlineData(9, 1, 10, 5, "future")]
    public async Task PeriodValidation(int startMonth, int startDay, int endMonth, int endDay, string expected)
    {
        var vm = Create();
        await vm.InitializeCommand.ExecuteAsync(null);

        vm.StartDate = D(startMonth, startDay);
        vm.EndDate = D(endMonth, endDay);

        Assert.Contains(expected, vm.PeriodError);
        Assert.False(vm.LoadCommand.CanExecute(null));
    }

    [Fact]
    public async Task PeriodLongerThanFiveYears_IsRejected()
    {
        var vm = Create();
        await vm.InitializeCommand.ExecuteAsync(null);

        vm.StartDate = new DateTime(2020, 1, 1);

        Assert.Contains("five years", vm.PeriodError);
    }

    [Fact]
    public async Task Presets_SetPeriodEndingToday()
    {
        var vm = Create();

        vm.ApplyPresetCommand.Execute(vm.PeriodPresets.Single(p => p.Days == 7));

        Assert.Equal(D(9, 27), vm.StartDate);
        Assert.Equal(D(10, 3), vm.EndDate);
        Assert.Equal("7 days", vm.PeriodText);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task NothingSelected_CannotLoad()
    {
        var vm = Create();
        await vm.InitializeCommand.ExecuteAsync(null);

        vm.ClearCurrenciesCommand.Execute(null);

        Assert.Equal(0, vm.SelectedCount);
        Assert.False(vm.LoadCommand.CanExecute(null));
    }

    [Fact]
    public async Task Load_FillsTableChartAndSaves()
    {
        var vm = Create();
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.StartDate = D(9, 1);
        vm.EndDate = D(9, 10);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(40, vm.Rows.Count);
        Assert.Equal(new DateTime(2026, 9, 10), vm.Rows[0].Date);
        Assert.Equal(["CNY", "EUR", "RUB", "USD"], vm.LoadedCurrencies);
        Assert.Equal("USD", vm.SelectedChartCurrency);
        Assert.Equal(10, vm.ChartPoints.Count);
        Assert.True(vm.ChartPoints.Zip(vm.ChartPoints.Skip(1)).All(pair => pair.First.Date < pair.Second.Date));
        Assert.Equal(10, vm.Statistics!.Count);
        Assert.Equal(40, _store.Saved!.Records.Count);
        Assert.False(vm.HasUnsavedChanges);
        Assert.True(vm.ExportCsvCommand.CanExecute(null));
        Assert.Equal(4, vm.ProgressValue);
    }

    [Fact]
    public async Task Rows_KnowDayOverDayTrend()
    {
        var vm = Create();
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.StartDate = D(9, 1);
        vm.EndDate = D(9, 2);
        await vm.LoadCommand.ExecuteAsync(null);

        var usd = vm.Rows.Where(row => row.Code == "USD").OrderBy(row => row.Date).ToList();

        Assert.Null(usd[0].ChangePercent);
        Assert.Equal(0, usd[0].Trend);
        Assert.Equal(1, usd[1].Trend);
    }

    [Fact]
    public async Task EditingRate_MarksUnsavedAndUpdatesChart()
    {
        var vm = Create();
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.StartDate = D(9, 1);
        vm.EndDate = D(9, 3);
        await vm.LoadCommand.ExecuteAsync(null);
        var row = vm.Rows.First(r => r.Code == "USD");

        row.Rate = 9.99m;

        Assert.True(row.IsEdited);
        Assert.True(vm.HasUnsavedChanges);
        Assert.Equal(9.99m, vm.Statistics!.Max.Rate);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.HasUnsavedChanges);
        Assert.Contains(_store.Saved!.Records, record => record.Rate == 9.99m);
    }

    [Fact]
    public async Task EditingRate_RejectsNonPositiveValues()
    {
        var vm = Create();
        await vm.InitializeCommand.ExecuteAsync(null);
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Throws<ArgumentOutOfRangeException>(() => vm.Rows[0].Rate = 0);
    }

    [Fact]
    public async Task TableSearch_FiltersRows()
    {
        var vm = Create();
        await vm.InitializeCommand.ExecuteAsync(null);
        vm.StartDate = D(9, 1);
        vm.EndDate = D(9, 5);
        await vm.LoadCommand.ExecuteAsync(null);

        vm.TableSearch = "eur";

        Assert.Equal(5, vm.Rows.Count);
        Assert.All(vm.Rows, row => Assert.Equal("EUR", row.Code));
        Assert.Equal("5 of 20 rates", vm.RowsSummary);
    }

    [Fact]
    public async Task CurrencySearch_FiltersPicker()
    {
        var vm = Create();
        await vm.InitializeCommand.ExecuteAsync(null);

        vm.CurrencySearch = "pl";

        Assert.Equal("PLN", Assert.Single(vm.VisibleCurrencies).Code);

        vm.SelectAllCurrenciesCommand.Execute(null);
        Assert.Equal(5, vm.SelectedCount);
    }

    [Fact]
    public async Task ApiError_IsShownAndKeepsPreviousData()
    {
        var vm = Create();
        await vm.InitializeCommand.ExecuteAsync(null);
        await vm.LoadCommand.ExecuteAsync(null);
        var before = vm.Rows.Count;
        _client.DynamicsError = new ExchangeRateApiException("Service unavailable");

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(before, vm.Rows.Count);
        Assert.Contains("Service unavailable", Assert.Single(_dialogs.Errors));
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Load_CanBeCancelled()
    {
        var vm = Create();
        await vm.InitializeCommand.ExecuteAsync(null);
        _client.Gate = new TaskCompletionSource();

        var loading = vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        vm.LoadCancelCommand.Execute(null);
        await loading;

        Assert.False(vm.IsBusy);
        Assert.Equal("Loading cancelled.", vm.StatusMessage);
        Assert.False(vm.HasRows);
    }

    [Fact]
    public async Task ExportCsv_WritesVisibleRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"cea-{Guid.NewGuid():N}.csv");
        try
        {
            var vm = Create();
            await vm.InitializeCommand.ExecuteAsync(null);
            vm.StartDate = D(9, 1);
            vm.EndDate = D(9, 2);
            await vm.LoadCommand.ExecuteAsync(null);
            vm.TableSearch = "USD";
            _dialogs.CsvPath = path;

            await vm.ExportCsvCommand.ExecuteAsync(null);

            var lines = await File.ReadAllLinesAsync(path);
            Assert.Equal(3, lines.Length);
            Assert.StartsWith("Date;", lines[0].TrimStart('﻿'));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
