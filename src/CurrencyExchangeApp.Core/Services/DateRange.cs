namespace CurrencyExchangeApp.Core.Services;

/// <summary>An inclusive range of calendar days (times of day are ignored).</summary>
public readonly record struct DateRange(DateTime Start, DateTime End)
{
    public int Days => (End.Date - Start.Date).Days + 1;

    /// <summary>Splits the range into consecutive pieces of at most <paramref name="maxDays"/> days.</summary>
    public IEnumerable<DateRange> Split(int maxDays)
    {
        if (maxDays < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDays));
        }

        if (End < Start)
        {
            yield break;
        }

        for (var chunkStart = Start; chunkStart <= End; chunkStart = chunkStart.AddDays(maxDays))
        {
            var chunkEnd = chunkStart.AddDays(maxDays - 1);
            yield return new DateRange(chunkStart, chunkEnd < End ? chunkEnd : End);
        }
    }

    public override string ToString() => $"{Start:dd.MM.yyyy} – {End:dd.MM.yyyy}";
}
