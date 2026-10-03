namespace CurrencyExchangeApp.Core.Models;

/// <summary>A currency quoted by the National Bank of the Republic of Belarus.</summary>
/// <param name="Id">NBRB internal currency id, used to request rate history.</param>
/// <param name="Code">ISO 4217 letter code, e.g. USD.</param>
/// <param name="Name">Display name.</param>
/// <param name="Scale">Number of units the official rate is quoted for (e.g. 100 RUB).</param>
public sealed record Currency(int Id, string Code, string Name, int Scale);

/// <summary>Official rate of one currency on one day, in Belarusian rubles per <see cref="Currency.Scale"/> units.</summary>
public readonly record struct RatePoint(DateTime Date, decimal Rate);

/// <summary>A row of the rates table; this is also the shape stored on disk.</summary>
public sealed record RateRecord(DateTime Date, int CurrencyId, string Code, string Name, int Scale, decimal Rate);

/// <summary>Rates saved between sessions.</summary>
public sealed class RateSnapshot
{
    public DateTimeOffset SavedAt { get; set; }

    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    public List<RateRecord> Records { get; set; } = [];
}
