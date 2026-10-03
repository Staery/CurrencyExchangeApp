using CurrencyExchangeApp.Core.Models;

namespace CurrencyExchangeApp.Core.Services;

/// <summary>Source of official exchange rates.</summary>
public interface IExchangeRateClient
{
    /// <summary>Currencies with a daily official rate.</summary>
    Task<IReadOnlyList<Currency>> GetDailyCurrenciesAsync(CancellationToken cancellationToken = default);

    /// <summary>Official rates of one currency for every day in the inclusive range.</summary>
    Task<IReadOnlyList<RatePoint>> GetDynamicsAsync(int currencyId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
}

/// <summary>The rate service could not be reached or returned something unexpected.</summary>
public sealed class ExchangeRateApiException(string message, Exception? innerException = null)
    : Exception(message, innerException);
