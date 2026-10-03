using System.Globalization;
using System.Windows.Data;

namespace CurrencyExchangeApp.Converters;

/// <summary>Converts a number to -1, 0 or 1, for colouring rises and falls.</summary>
public sealed class SignConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        decimal number => Math.Sign(number),
        double number => Math.Sign(number),
        int number => Math.Sign(number),
        _ => 0,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
