using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StockSimulator.Models
{
    public class Stock
    {
        public string symbol { get; set; }
        public int sharesBought { get; set; }
        public int sharesOwned { get; set; }
        public DateTime date { get; set; }
        public double price { get; set; }
    }
}
