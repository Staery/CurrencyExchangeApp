using System.Net;
using System.Text;
using CurrencyExchangeApp.Core.Abstractions;
using CurrencyExchangeApp.Core.Models;
using CurrencyExchangeApp.Core.Services;

namespace CurrencyExchangeApp.Core.Tests;

/// <summary>Answers HTTP requests from a lambda and records the requested URLs.</summary>
internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (Requests)
        {
            Requests.Add(request.RequestUri!.PathAndQuery);
        }

        return Task.FromResult(respond(request));
    }
}

internal sealed class FakeClient : IExchangeRateClient
{
    public List<Currency> Currencies { get; } =
    [
        new(431, "USD", "Доллар США", 1),
        new(451, "EUR", "Евро", 1),
        new(456, "RUB", "Российских рублей", 100),
        new(462, "CNY", "Китайских юаней", 10),
        new(426, "PLN", "Злотых", 10),
    ];

    public Exception? CurrenciesError { get; set; }

    public Exception? DynamicsError { get; set; }

    public TaskCompletionSource? Gate { get; set; }

    public Task<IReadOnlyList<Currency>> GetDailyCurrenciesAsync(CancellationToken cancellationToken = default) =>
        CurrenciesError is null ? Task.FromResult<IReadOnlyList<Currency>>(Currencies) : Task.FromException<IReadOnlyList<Currency>>(CurrenciesError);

    public async Task<IReadOnlyList<RatePoint>> GetDynamicsAsync(int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default)
    {
        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken);
        }

        if (DynamicsError is not null)
        {
            throw DynamicsError;
        }

        var points = new List<RatePoint>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            points.Add(new RatePoint(day, currencyId / 100m + day.Day / 1000m));
        }

        return points;
    }
}

internal sealed class MemoryStore : IRateStore
{
    public RateSnapshot? Saved { get; set; }

    public int SaveCount { get; private set; }

    public string Location => "memory://rates.json";

    public Task<RateSnapshot?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Saved);

    public Task SaveAsync(RateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        SaveCount++;
        Saved = snapshot;
        return Task.CompletedTask;
    }
}

internal sealed class FakeDialogs : IDialogService
{
    public List<string> Errors { get; } = [];

    public string? CsvPath { get; set; }

    public void ShowError(string title, string message) => Errors.Add(message);

    public string? PickCsvSavePath(string suggestedFileName) => CsvPath;
}

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
