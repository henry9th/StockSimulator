using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StockSimulator.Models
{
    public class PortfolioResult
    {
        public IEnumerable<IEnumerable<Stock>> allStockPriceData { get; set; }
        public IEnumerable<NetWorthDataPoint> networthDataPoints { get; set; }

    }
}
