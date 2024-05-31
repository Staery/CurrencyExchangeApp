using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CurrencyExchangeApp.Models;
using CurrencyExchangeApp.Services;
using CurrencyExchangeApp.Commands;
using System.Collections.Generic;
using System.Linq;

namespace CurrencyExchangeApp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly CurrencyAPIService _currencyAPIService;
        private readonly FileService _fileService;
        private DateTime _startDate;
        private DateTime _endDate;

        public MainViewModel()
        {
            _currencyAPIService = new CurrencyAPIService();
            _fileService = new FileService("data.json");
            SetDefaultDateRange();
            InitializeCommands();
            LoadSavedData();
        }

        public ObservableCollection<Rate> Currencies { get; } = new ObservableCollection<Rate>();
        public Rate SelectedCurrency { get; set; }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                _startDate = value;
                OnPropertyChanged();
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                _endDate = value;
                OnPropertyChanged();
            }
        }

        public ICommand LoadDataCommand { get; private set; }
        public ICommand SaveDataCommand { get; private set; }

        private bool _isBusy = false;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;

                if (!IsValidDateRange())
                {
                    MessageBox.Show("Пожалуйста, выберите корректные даты.");
                    return;
                }

                var currencies = await _currencyAPIService.GetCurrencies();
                await LoadCurrencyDataAsync(currencies);
                _fileService.UpdateDataInFile(Currencies.ToList());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadCurrencyDataAsync(List<Rate> currencies)
        {
            Currencies.Clear();
            foreach (var currency in currencies)
            {
                var dynamics = await _currencyAPIService.GetCurrencyDynamics(currency.Cur_ID, _startDate, _endDate);
                foreach (var dynamicData in dynamics)
                {
                    var rateWithDynamics = new Rate
                    {
                        Date = dynamicData.Date,
                        Cur_Abbreviation = currency.Cur_Abbreviation,
                        Cur_Name = currency.Cur_Name,
                        Cur_OfficialRate = dynamicData.Cur_OfficialRate,
                    };
                    rateWithDynamics.RateDataChanged += OnRateDataChanged;
                    Currencies.Add(rateWithDynamics);
                }
            }
        }

        private void OnRateDataChanged(object sender, EventArgs e)
        {
            _fileService.UpdateDataInFile(Currencies.ToList());
        }

        private void LoadSavedData()
        {
            var loadedData = _fileService.ReadDataFromFile();
            foreach (var loadedRate in loadedData)
            {
                loadedRate.RateDataChanged += OnRateDataChanged;
                Currencies.Add(loadedRate);
            }
        }

        private void SetDefaultDateRange()
        {
            _startDate = DateTime.Now.AddDays(-365);
            _endDate = DateTime.Now;
        }

        private bool IsValidDateRange()
        {
            return _startDate != DateTime.MinValue && _endDate != DateTime.MinValue && _startDate <= _endDate;
        }

        private void InitializeCommands()
        {
            LoadDataCommand = new RelayCommand(async () => await LoadDataAsync(), () => !IsBusy);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
