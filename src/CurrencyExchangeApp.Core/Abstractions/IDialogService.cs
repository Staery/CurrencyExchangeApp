namespace CurrencyExchangeApp.Core.Abstractions;

/// <summary>User interaction that view models need but must not implement themselves.</summary>
public interface IDialogService
{
    void ShowError(string title, string message);

    /// <summary>Asks where to save a CSV file; returns <see langword="null"/> if the user cancelled.</summary>
    string? PickCsvSavePath(string suggestedFileName);
}
