using CurrencyExchangeApp.Core.Models;

namespace CurrencyExchangeApp.Core.Services;

/// <summary>Summary of a rate series, shown above the chart.</summary>
public sealed record RateStatistics(
    RatePoint First,
    RatePoint Last,
    RatePoint Min,
    RatePoint Max,
    decimal Average,
    int Count)
{
    public decimal Change => Last.Rate - First.Rate;

    public decimal? ChangePercent => First.Rate == 0 ? null : Change / First.Rate * 100;

    /// <summary>Returns <see langword="null"/> for an empty series.</summary>
    public static RateStatistics? Calculate(IEnumerable<RatePoint> points)
    {
        var ordered = points.OrderBy(point => point.Date).ToList();
        if (ordered.Count == 0)
        {
            return null;
        }

        var min = ordered[0];
        var max = ordered[0];
        decimal sum = 0;

        foreach (var point in ordered)
        {
            if (point.Rate < min.Rate)
            {
                min = point;
            }

            if (point.Rate > max.Rate)
            {
                max = point;
            }

            sum += point.Rate;
        }

        return new RateStatistics(ordered[0], ordered[ordered.Count - 1], min, max, Math.Round(sum / ordered.Count, 4), ordered.Count);
    }
}
