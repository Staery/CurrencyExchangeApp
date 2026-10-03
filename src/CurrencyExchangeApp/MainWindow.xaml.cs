using System.Windows;
using CurrencyExchangeApp.Core.ViewModels;

namespace CurrencyExchangeApp;

/// <summary>Main window. All behaviour lives in <see cref="MainViewModel"/>.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e) => await _viewModel.InitializeCommand.ExecuteAsync(null);
}
