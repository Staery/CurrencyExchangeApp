using System.Globalization;
using CurrencyExchangeApp.Core.Models;

namespace CurrencyExchangeApp.Core.Services;

/// <summary>Writes rates as CSV that opens correctly in Excel (semicolon-separated, UTF-8 with BOM).</summary>
public static class CsvExporter
{
    public const string Header = "Date;Code;Currency;Scale;Rate (BYN)";

    public static void Write(TextWriter writer, IEnumerable<RateRecord> records)
    {
        Guard.NotNull(writer, nameof(writer));
        Guard.NotNull(records, nameof(records));

        writer.WriteLine(Header);

        foreach (var record in records)
        {
            writer.WriteLine(string.Join(";",
                record.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Escape(record.Code),
                Escape(record.Name),
                record.Scale.ToString(CultureInfo.InvariantCulture),
                record.Rate.ToString("0.####", CultureInfo.InvariantCulture)));
        }
    }

    public static async Task ExportAsync(string path, IEnumerable<RateRecord> records, CancellationToken cancellationToken = default)
    {
        using var writer = new StreamWriter(path, append: false, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Write(writer, records);
        cancellationToken.ThrowIfCancellationRequested();
        await writer.FlushAsync().ConfigureAwait(false);
    }

    private static string Escape(string value) =>
        value.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
