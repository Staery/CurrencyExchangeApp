using CurrencyExchangeApp.Core.Models;
using CurrencyExchangeApp.Core.Services;

namespace CurrencyExchangeApp.Core.Tests;

public class ServicesTests
{
    private static DateTime D(int month, int day) => new(2026, month, day);

    [Fact]
    public void DateRange_SplitCoversRangeWithoutGapsOrOverlaps()
    {
        var range = new DateRange(D(1, 1), D(3, 15));

        var chunks = range.Split(30).ToList();

        Assert.Equal(range.Start, chunks[0].Start);
        Assert.Equal(range.End, chunks[^1].End);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Days, 1, 30));
        Assert.All(chunks.Zip(chunks.Skip(1)), pair => Assert.Equal(pair.First.End.AddDays(1), pair.Second.Start));
        Assert.Equal(range.Days, chunks.Sum(chunk => chunk.Days));
    }

    [Fact]
    public void DateRange_EmptyWhenReversed() => Assert.Empty(new DateRange(D(2, 1), D(1, 1)).Split(10));

    [Fact]
    public void Statistics_SummarizesSeries()
    {
        var stats = RateStatistics.Calculate(
        [
            new RatePoint(D(1, 3), 3.0m),
            new RatePoint(D(1, 1), 2.0m),
            new RatePoint(D(1, 2), 4.0m),
        ]);

        Assert.NotNull(stats);
        Assert.Equal(D(1, 1), stats.First.Date);
        Assert.Equal(3.0m, stats.Last.Rate);
        Assert.Equal(new RatePoint(D(1, 1), 2.0m), stats.Min);
        Assert.Equal(new RatePoint(D(1, 2), 4.0m), stats.Max);
        Assert.Equal(3.0m, stats.Average);
        Assert.Equal(1.0m, stats.Change);
        Assert.Equal(50m, stats.ChangePercent);
        Assert.Equal(3, stats.Count);
    }

    [Fact]
    public void Statistics_NullForEmptySeries() => Assert.Null(RateStatistics.Calculate([]));

    [Fact]
    public void Csv_UsesInvariantFormatAndEscapesText()
    {
        var writer = new StringWriter();

        CsvExporter.Write(writer,
        [
            new RateRecord(D(9, 1), 431, "USD", "Доллар США", 1, 2.9483m),
            new RateRecord(D(9, 1), 1, "XXX", "Name; with \"quotes\"", 10, 12m),
        ]);

        var lines = writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(CsvExporter.Header, lines[0]);
        Assert.Equal("2026-09-01;USD;Доллар США;1;2.9483", lines[1]);
        Assert.Equal("2026-09-01;XXX;\"Name; with \"\"quotes\"\"\";10;12", lines[2]);
    }

    [Fact]
    public async Task Service_LoadsAllCurrenciesWithProgress()
    {
        var client = new FakeClient();
        var reported = new List<int>();

        var records = await new ExchangeRateService(client).LoadAsync(
            client.Currencies.Take(3).ToList(),
            new DateRange(D(9, 1), D(9, 5)),
            new SynchronousProgress(reported.Add));

        Assert.Equal(15, records.Count);
        Assert.Equal(["EUR", "RUB", "USD"], records.Select(record => record.Code).Distinct());
        Assert.Equal(100, records.First(record => record.Code == "RUB").Scale);
        Assert.Equal([1, 2, 3], reported.Order());
    }

    [Fact]
    public async Task Store_RoundTripsSnapshot()
    {
        var folder = Path.Combine(Path.GetTempPath(), "cea-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonRateStore(Path.Combine(folder, "rates.json"));
            var snapshot = new RateSnapshot
            {
                SavedAt = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero),
                Start = D(9, 1),
                End = D(9, 2),
                Records = [new RateRecord(D(9, 1), 431, "USD", "Доллар США", 1, 2.9483m)],
            };

            await store.SaveAsync(snapshot);
            var loaded = await store.LoadAsync();

            Assert.NotNull(loaded);
            Assert.Equal(snapshot.Start, loaded.Start);
            Assert.Equal(snapshot.Records, loaded.Records);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    private sealed class SynchronousProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value)
        {
            lock (this)
            {
                report(value);
            }
        }
    }
}
