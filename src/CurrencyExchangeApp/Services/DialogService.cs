using System.Windows;
using CurrencyExchangeApp.Core.Abstractions;
using Microsoft.Win32;

namespace CurrencyExchangeApp.Services;

/// <summary>Message boxes and the save dialog, owned by the main window.</summary>
internal sealed class DialogService : IDialogService
{
    private static Window? Owner => Application.Current?.MainWindow is { IsVisible: true } window ? window : null;

    public void ShowError(string title, string message)
    {
        if (Owner is { } owner)
        {
            MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public string? PickCsvSavePath(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export rates",
            FileName = suggestedFileName,
            DefaultExt = ".csv",
            Filter = "CSV file (*.csv)|*.csv",
        };

        var owner = Owner;
        var accepted = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return accepted == true ? dialog.FileName : null;
    }
}
