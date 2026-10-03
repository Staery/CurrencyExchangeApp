using CommunityToolkit.Mvvm.ComponentModel;
using CurrencyExchangeApp.Core.Models;

namespace CurrencyExchangeApp.Core.ViewModels;

/// <summary>A currency in the picker.</summary>
public sealed partial class CurrencyOptionViewModel(Currency currency) : ObservableObject
{
    public Currency Currency { get; } = currency;

    public string Code => Currency.Code;

    public string Name => Currency.Name;

    public string ScaleText => Currency.Scale == 1 ? string.Empty : $"per {Currency.Scale}";

    [ObservableProperty]
    private bool _isSelected;
}
