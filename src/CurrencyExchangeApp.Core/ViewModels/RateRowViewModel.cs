using CommunityToolkit.Mvvm.ComponentModel;
using CurrencyExchangeApp.Core.Models;

namespace CurrencyExchangeApp.Core.ViewModels;

/// <summary>A row of the rates table. The rate can be corrected by hand.</summary>
public sealed partial class RateRowViewModel : ObservableObject
{
    private readonly RateRecord _original;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEdited), nameof(ChangePercent), nameof(ChangeText), nameof(Trend))]
    private decimal _rate;

    public RateRowViewModel(RateRecord record, decimal? previousRate)
    {
        _original = record;
        _rate = record.Rate;
        PreviousRate = previousRate;
    }

    public DateTime Date => _original.Date;


    public string Code => _original.Code;

    public string Name => _original.Name;

    public int Scale => _original.Scale;

    /// <summary>Rate of the same currency on the previous loaded day.</summary>
    public decimal? PreviousRate { get; }

    public bool IsEdited => Rate != _original.Rate;

    /// <summary>Day-over-day change in percent.</summary>
    public decimal? ChangePercent => PreviousRate is { } previous && previous != 0
        ? Math.Round((Rate - previous) / previous * 100, 2)
        : null;

    public string ChangeText => ChangePercent switch
    {
        null => "—",
        > 0 => $"▲ {ChangePercent:0.00}%",
        < 0 => $"▼ {-ChangePercent:0.00}%",
        _ => "0.00%",
    };

    /// <summary>1 when the rate went up, -1 when it went down, 0 otherwise.</summary>
    public int Trend => ChangePercent is { } change ? Math.Sign(change) : 0;

    public RateRecord ToRecord() => _original with { Rate = Rate };

    partial void OnRateChanging(decimal oldValue, decimal newValue)
    {
        if (newValue <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Rate), "The rate must be a positive number.");
        }
    }
}
