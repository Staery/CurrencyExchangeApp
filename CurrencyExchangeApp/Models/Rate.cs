using System;

namespace CurrencyExchangeApp.Models
{
    public class Rate
    {
        public event EventHandler RateDataChanged;
        private string _cur_Abbreviation;
        private decimal? _cur_OfficialRate;
        public int Cur_ID { get; set; }
        public DateTime Date { get; set; }
        public string Cur_Abbreviation
        {
            get => _cur_Abbreviation;
            set { _cur_Abbreviation = value; OnRateDataChanged(); }
        }
        public string Cur_Name { get; set; }
        public decimal? Cur_OfficialRate
        {
            get => _cur_OfficialRate;
            set { _cur_OfficialRate = value; OnRateDataChanged(); }
        }

        protected virtual void OnRateDataChanged()
        {
            RateDataChanged?.Invoke(this, EventArgs.Empty);
        }


    }
}
