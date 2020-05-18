using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StockSimulator.Models
{
    public class Stock
    {
        public string symbol { get; set; }
        public double sharesBought { get; set; } // allow partial stocks
        public double sharesOwned { get; set; }
        public DateTime date { get; set; }
        public double price { get; set; }
        public bool moneyFormat { get; set; }
        public double dividend { get; set; }
        public double sharesBoughtWithDividend { get; set; }
    }
}
