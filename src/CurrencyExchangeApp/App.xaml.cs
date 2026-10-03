using System.Windows;
using System.Windows.Threading;
using CurrencyExchangeApp.Core.Services;
using CurrencyExchangeApp.Core.ViewModels;
using CurrencyExchangeApp.Services;

namespace CurrencyExchangeApp;

/// <summary>Composition root: wires services and view models together and shows the main window.</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var viewModel = new MainViewModel(
            new NbrbClient(NbrbClient.CreateHttpClient()),
            JsonRateStore.CreateDefault(),
            new DialogService(),
            TimeProvider.System);

        MainWindow = new MainWindow(viewModel);
        MainWindow.Show();
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Something went wrong:\n\n{e.Exception.Message}", "Currency Exchange", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
