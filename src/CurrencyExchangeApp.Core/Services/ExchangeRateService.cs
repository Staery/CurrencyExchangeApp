using CurrencyExchangeApp.Core.Models;

namespace CurrencyExchangeApp.Core.Services;

/// <summary>Loads rate history for several currencies at once.</summary>
public sealed class ExchangeRateService(IExchangeRateClient client)
{
    /// <summary>Requests run in parallel, but politely: the public API is shared by everyone.</summary>
    public const int MaxConcurrentRequests = 4;

    /// <summary>Returns one record per currency and day, ordered by currency code and date.</summary>
    /// <param name="progress">Receives the number of currencies loaded so far.</param>
    public async Task<IReadOnlyList<RateRecord>> LoadAsync(
        IReadOnlyCollection<Currency> currencies,
        DateRange range,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(currencies, nameof(currencies));

        using var throttle = new SemaphoreSlim(MaxConcurrentRequests);
        var completed = 0;

        var tasks = currencies.Select(async currency =>
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var points = await client.GetDynamicsAsync(currency.Id, range.Start, range.End, cancellationToken).ConfigureAwait(false);
                progress?.Report(Interlocked.Increment(ref completed));

                return points.Select(point =>
                    new RateRecord(point.Date, currency.Id, currency.Code, currency.Name, currency.Scale, point.Rate));
            }
            finally
            {
                throttle.Release();
            }
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        return results
            .SelectMany(records => records)
            .OrderBy(record => record.Code, StringComparer.Ordinal)
            .ThenBy(record => record.Date)
            .ToList();
    }
}
