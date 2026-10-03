using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CurrencyExchangeApp.Converters;

/// <summary>Picks a brush for a trend of 1 (up), -1 (down) or 0.</summary>
public sealed class TrendBrushConverter : IValueConverter
{
    public Brush Up { get; set; } = Brushes.Green;

    public Brush Down { get; set; } = Brushes.Red;

    public Brush Flat { get; set; } = Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        > 0 => Up,
        < 0 => Down,
        _ => Flat,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
