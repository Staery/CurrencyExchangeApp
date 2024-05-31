using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyExchangeApp.Models
{
    public class RateShort
    {
        public DateTime Date { get; set; }
        public decimal? Cur_OfficialRate { get; set; }
    }
}
