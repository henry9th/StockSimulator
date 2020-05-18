using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StockSimulator.Models
{
    public class NetWorthDataPoint
    {
        public DateTime date { get; set; }
        public double totalValue { get; set; }
        public double totalCost { get; set; }
        public double subValue { get; set; }
        public double subCost { get; set; }
        public double differenceFromCost { get; set; }
        public double dividendEarned { get; set; }
        public double valueOfSharesFromDividend { get; set; }
        public string note { get; set; }
    }
}
