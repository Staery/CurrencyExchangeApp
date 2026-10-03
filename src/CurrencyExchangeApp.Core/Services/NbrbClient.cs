using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CurrencyExchangeApp.Core.Models;

namespace CurrencyExchangeApp.Core.Services;

/// <summary>
/// Client for the public API of the National Bank of the Republic of Belarus
/// (<see href="https://www.nbrb.by/apihelp/exrates"/>).
/// </summary>
public sealed class NbrbClient(HttpClient http) : IExchangeRateClient
{
    public static readonly Uri DefaultBaseAddress = new("https://api.nbrb.by/");

    /// <summary>The API refuses rate history requests longer than a year.</summary>
    public const int MaxDaysPerRequest = 365;

    public static HttpClient CreateHttpClient() => new()
    {
        BaseAddress = DefaultBaseAddress,
        Timeout = TimeSpan.FromSeconds(30),
    };

    public async Task<IReadOnlyList<Currency>> GetDailyCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        var rates = await GetAsync<List<RateDto>>("exrates/rates?periodicity=0", cancellationToken).ConfigureAwait(false);
        var englishNames = await GetEnglishNamesAsync(cancellationToken).ConfigureAwait(false);

        return rates
            .Where(rate => !string.IsNullOrWhiteSpace(rate.Abbreviation))
            .Select(rate => new Currency(
                rate.Id,
                rate.Abbreviation!.Trim(),
                (englishNames.TryGetValue(rate.Id, out var english) ? english : null) ?? rate.Name?.Trim() ?? rate.Abbreviation,
                Math.Max(1, rate.Scale)))
            .OrderBy(currency => currency.Code, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>English currency names by id. The rates endpoint only returns Russian names; this lookup is optional.</summary>
    private async Task<Dictionary<int, string>> GetEnglishNamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var currencies = await GetAsync<List<CurrencyDto>>("exrates/currencies", cancellationToken).ConfigureAwait(false);

            return currencies
                .Where(currency => !string.IsNullOrWhiteSpace(currency.NameEng))
                .GroupBy(currency => currency.Id)
                .ToDictionary(group => group.Key, group => group.First().NameEng!.Trim());
        }
        catch (ExchangeRateApiException)
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<RatePoint>> GetDynamicsAsync(
        int currencyId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default)
    {
        var points = new List<RatePoint>();

        foreach (var chunk in new DateRange(start, end).Split(MaxDaysPerRequest))
        {
            var url = string.Format(
                CultureInfo.InvariantCulture,
                "exrates/rates/dynamics/{0}?startdate={1:yyyy-MM-dd}&enddate={2:yyyy-MM-dd}",
                currencyId,
                chunk.Start,
                chunk.End);

            var rates = await GetAsync<List<RateShortDto>>(url, cancellationToken).ConfigureAwait(false);

            points.AddRange(rates
                .Where(rate => rate.OfficialRate is not null)
                .Select(rate => new RatePoint(rate.Date.Date, rate.OfficialRate!.Value)));
        }

        return points.OrderBy(point => point.Date).ToList();
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ExchangeRateApiException("The National Bank has no rates for this request.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ExchangeRateApiException(
                    $"The National Bank API returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false)
                ?? throw new ExchangeRateApiException("The National Bank API returned an empty response.");
        }
        catch (HttpRequestException ex)
        {
            throw new ExchangeRateApiException($"Could not reach api.nbrb.by: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExchangeRateApiException("The National Bank API did not respond in time.", ex);
        }
        catch (JsonException ex)
        {
            throw new ExchangeRateApiException("The National Bank API returned data in an unexpected format.", ex);
        }
    }

    private sealed record RateDto(
        [property: JsonPropertyName("Cur_ID")] int Id,
        [property: JsonPropertyName("Cur_Abbreviation")] string? Abbreviation,
        [property: JsonPropertyName("Cur_Scale")] int Scale,
        [property: JsonPropertyName("Cur_Name")] string? Name);

    private sealed record CurrencyDto(
        [property: JsonPropertyName("Cur_ID")] int Id,
        [property: JsonPropertyName("Cur_Name_Eng")] string? NameEng);

    private sealed record RateShortDto(
        [property: JsonPropertyName("Date")] DateTime Date,
        [property: JsonPropertyName("Cur_OfficialRate")] decimal? OfficialRate);
}
