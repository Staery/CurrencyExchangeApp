using System.Text.Json;
using CurrencyExchangeApp.Core.Models;

namespace CurrencyExchangeApp.Core.Services;

/// <summary>Keeps the last loaded (and possibly edited) rates between sessions.</summary>
public interface IRateStore
{
    string Location { get; }

    Task<RateSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(RateSnapshot snapshot, CancellationToken cancellationToken = default);
}

/// <summary>Stores rates as an indented JSON file, written atomically.</summary>
public sealed class JsonRateStore(string filePath) : IRateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Location { get; } = Path.GetFullPath(filePath);

    public static JsonRateStore CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CurrencyExchangeApp",
        "rates.json"));

    public async Task<RateSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Location))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(Location);
            var snapshot = await JsonSerializer.DeserializeAsync<RateSnapshot>(stream, Options, cancellationToken).ConfigureAwait(false);
            if (snapshot is not null)
            {
                snapshot.Records = (snapshot.Records ?? []).Where(record => record is not null).ToList();
            }

            return snapshot;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The saved rates file '{Location}' is damaged: {ex.Message}", ex);
        }
    }

    public async Task SaveAsync(RateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(snapshot, nameof(snapshot));

        Directory.CreateDirectory(Path.GetDirectoryName(Location)!);
        var tempPath = Location + ".tmp";

        using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, Options, cancellationToken).ConfigureAwait(false);
        }

        if (File.Exists(Location))
        {
            File.Replace(tempPath, Location, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, Location);
        }
    }
}
