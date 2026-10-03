using System.Net;
using CurrencyExchangeApp.Core.Models;
using CurrencyExchangeApp.Core.Services;

namespace CurrencyExchangeApp.Core.Tests;

public class NbrbClientTests
{
    private static NbrbClient Create(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = NbrbClient.DefaultBaseAddress });

    [Fact]
    public async Task GetDailyCurrencies_ParsesNbrbFormat()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("currencies")
            ? StubHandler.Json("""[{"Cur_ID":431,"Cur_Name_Eng":"US Dollar"},{"Cur_ID":999,"Cur_Name_Eng":"Old"}]""")
            : StubHandler.Json("""
            [
              {"Cur_ID":456,"Date":"2026-10-03T00:00:00","Cur_Abbreviation":"RUB","Cur_Scale":100,"Cur_Name":"Российских рублей","Cur_OfficialRate":3.5104},
              {"Cur_ID":431,"Date":"2026-10-03T00:00:00","Cur_Abbreviation":"USD","Cur_Scale":1,"Cur_Name":"Доллар США","Cur_OfficialRate":2.9483},
              {"Cur_ID":1,"Date":"2026-10-03T00:00:00","Cur_Abbreviation":null,"Cur_Scale":1,"Cur_Name":"Broken","Cur_OfficialRate":1}
            ]
            """));

        var currencies = await Create(handler).GetDailyCurrenciesAsync();

        // English names are used where the currency list has them, Russian ones otherwise.
        Assert.Equal(
            [new Currency(456, "RUB", "Российских рублей", 100), new Currency(431, "USD", "US Dollar", 1)],
            currencies);
        Assert.Equal(["/exrates/rates?periodicity=0", "/exrates/currencies"], handler.Requests);
    }

    [Fact]
    public async Task GetDailyCurrencies_WorksWhenNameLookupFails()
    {
        var handler = new StubHandler(request => request.RequestUri!.AbsolutePath.EndsWith("currencies")
            ? StubHandler.Json("{}", HttpStatusCode.InternalServerError)
            : StubHandler.Json("""[{"Cur_ID":431,"Cur_Abbreviation":"USD","Cur_Scale":1,"Cur_Name":"Доллар США"}]"""));

        var currencies = await Create(handler).GetDailyCurrenciesAsync();

        Assert.Equal("Доллар США", Assert.Single(currencies).Name);
    }

    [Fact]
    public async Task GetDynamics_ParsesAndSortsPoints()
    {
        var handler = new StubHandler(_ => StubHandler.Json("""
            [
              {"Cur_ID":431,"Date":"2026-09-02T00:00:00","Cur_OfficialRate":2.95},
              {"Cur_ID":431,"Date":"2026-09-01T00:00:00","Cur_OfficialRate":2.9411},
              {"Cur_ID":431,"Date":"2026-09-03T00:00:00","Cur_OfficialRate":null}
            ]
            """));

        var points = await Create(handler).GetDynamicsAsync(431, new DateTime(2026, 9, 1), new DateTime(2026, 9, 3));

        Assert.Equal([new RatePoint(new DateTime(2026, 9, 1), 2.9411m), new RatePoint(new DateTime(2026, 9, 2), 2.95m)], points);
        Assert.Equal("/exrates/rates/dynamics/431?startdate=2026-09-01&enddate=2026-09-03", Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task GetDynamics_SplitsPeriodsLongerThanAYear()
    {
        var handler = new StubHandler(_ => StubHandler.Json("[]"));

        await Create(handler).GetDynamicsAsync(431, new DateTime(2024, 1, 1), new DateTime(2025, 12, 31));

        Assert.Equal(
            [
                "/exrates/rates/dynamics/431?startdate=2024-01-01&enddate=2024-12-30",
                "/exrates/rates/dynamics/431?startdate=2024-12-31&enddate=2025-12-30",
                "/exrates/rates/dynamics/431?startdate=2025-12-31&enddate=2025-12-31",
            ],
            handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "500")]
    [InlineData(HttpStatusCode.NotFound, "no rates")]
    public async Task HttpErrors_BecomeFriendlyExceptions(HttpStatusCode status, string expected)
    {
        var handler = new StubHandler(_ => StubHandler.Json("{}", status));

        var error = await Assert.ThrowsAsync<ExchangeRateApiException>(() => Create(handler).GetDailyCurrenciesAsync());

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public async Task InvalidJson_BecomesFriendlyException()
    {
        var handler = new StubHandler(_ => StubHandler.Json("<html>maintenance</html>"));

        var error = await Assert.ThrowsAsync<ExchangeRateApiException>(() => Create(handler).GetDailyCurrenciesAsync());

        Assert.Contains("unexpected format", error.Message);
    }

    [Fact]
    public async Task NetworkFailure_BecomesFriendlyException()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("No such host is known."));

        var error = await Assert.ThrowsAsync<ExchangeRateApiException>(() => Create(handler).GetDailyCurrenciesAsync());

        Assert.Contains("Could not reach", error.Message);
    }

    [Fact]
    public async Task Cancellation_IsNotReportedAsApiError()
    {
        var handler = new StubHandler(_ => StubHandler.Json("[]"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(handler).GetDailyCurrenciesAsync(cts.Token));
    }
}
