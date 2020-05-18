using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StockSimulator.Models;

namespace StockSimulator.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // Given a list of stock purchases, it will simply return a dictionary that maps the symbol to the OLDEST purchase date
        public Dictionary<string, DateTime> getUniqueSymbols(IEnumerable<Stock> inputDataList) 
        {
            var uniqueSymbols = new Dictionary<string, DateTime>();
            foreach (Stock data in inputDataList)
            {
                if (uniqueSymbols.ContainsKey(data.symbol))
                {
                    if (data.date.CompareTo(uniqueSymbols.GetValueOrDefault(data.symbol)) < 0)
                    {
                        uniqueSymbols.Remove(data.symbol);
                        uniqueSymbols.Add(data.symbol, data.date);
                    }
                }
                else
                {
                    uniqueSymbols.Add(data.symbol, data.date);
                }
            }

            return uniqueSymbols;
        }

        public IList<Stock> getPriceDataAndCombineWithInput(string symbol, IList<Stock> inputDataList, Dictionary<string, DateTime> uniqueSymbols, bool moneyFormat, bool reinvestDividends)
        {
            IEnumerable<JToken> rawData = getAVStockInfo(symbol).Reverse();

            // Only want data after oldest purchase price 
            IList<Stock> stockData = rawData.Where(data =>
            {
                var dateName = ((JProperty)data).Name;

                DateTime date = DateTime.Parse(dateName);

                return (date.CompareTo(uniqueSymbols.GetValueOrDefault(symbol)) >= 0);

            }).Select(data =>
            {
                var price = data.First.Value<double>("5. adjusted close");
                var dividend = data.First.Value<double>("7. dividend amount");
                var sharesBoughtWithDividend = 0;

                foreach (Stock d in inputDataList)
                {
                    // if the stock object already exists in our input array, just add the price and return that 
                    if (DateTime.Parse(((JProperty)data).Name) == d.date && symbol == d.symbol)
                    {
                        if (moneyFormat)
                        {
                            var money = d.sharesBought;
                            double actualSharesBought = (money / price);
                            d.sharesBought = actualSharesBought;
                        }

                        inputDataList.Remove(d);

                        return new Stock
                        {
                            symbol = symbol,
                            sharesBought = d.sharesBought,
                            date = DateTime.Parse(((JProperty)data).Name),
                            price = price,
                            moneyFormat = moneyFormat,
                            dividend = dividend,
                            sharesBoughtWithDividend = 0
                        };
                    }
                }

                // else return with numShares being 0 as it will help build charts
                return new Stock
                {
                    symbol = symbol,
                    sharesBought = 0,
                    date = DateTime.Parse(((JProperty)data).Name),
                    price = price,
                    dividend = dividend,
                    moneyFormat = moneyFormat,
                    sharesBoughtWithDividend = 0
                };
            }).ToList();

            // list of the input stock that matches the symbol we're currrently interested in 
            IList<Stock> relevantInput = inputDataList.Where(stock =>
            {
                return (stock.symbol == symbol);
            }).ToList();

            // handle the leftover dates (dates that do not fall on a day that the market was open)
            while (relevantInput.Count() > 0 && relevantInput.First().date.CompareTo(DateTime.Today) < 0)
            {
                Stock d = relevantInput.First();
                d.date = d.date.AddDays(1);

                for (int i = 0; i < stockData.Count; i++)
                {
                    var stock = stockData[i];

                    if (stock.date == d.date && stock.symbol == d.symbol)
                    {
                        if (moneyFormat)
                        {
                            var money = d.sharesBought;
                            double actualSharesBought = (money / stock.price);
                            d.sharesBought = actualSharesBought;
                        }

                        relevantInput.Remove(d);
                        stock.sharesBought = d.sharesBought;
                    }
                }

                // error hit (date is past target date)
            }

            // finally populate the sharesOwned for each stock (we can't do this earlier because of how we handle leftovers) 
            // if dividends are to be reinvested, convert the dividends field to sharesFromDividends
            double sharesOwned = 0;
            double totalSharesBoughtWithDividends = 0;
            for (int i = 0; i < stockData.Count; i++)
            {
                Stock stock = stockData[i];
                sharesOwned += stock.sharesBought;
                stock.sharesOwned = sharesOwned;

                if (reinvestDividends && stock.dividend > 0)
                {
                    double dividendMoney = stock.dividend * stock.sharesOwned;
                    double sharesBoughtWithDividends = dividendMoney / stock.price;
                    totalSharesBoughtWithDividends += sharesBoughtWithDividends;

                    stock.dividend = 0;
                }

                stock.sharesBoughtWithDividend = totalSharesBoughtWithDividends;
            }

            return stockData;
        }

        public IEnumerable<NetWorthDataPoint> getNetworthDataPoints(IList<IList<Stock>> allStockPriceData, bool reinvestDividends)
        {
            // note that each list of stock information should be sorted from newest date to old 

            List<NetWorthDataPoint> networthDataPoints = new List<NetWorthDataPoint>();

            DateTime dateIndex = DateTime.MaxValue;

            foreach (IEnumerable<Stock> stockPriceData in allStockPriceData)
            {
                if (stockPriceData.ElementAt(0).date.CompareTo(dateIndex) < 0)
                {
                    dateIndex = stockPriceData.ElementAt(0).date; 
                }
            }

            double totalCost = 0;
            double totalValue = 0;
            double dividendsEarned = 0;
            double totalValueOfSharesFromDividends = 0;

            int finishCount = 0;
            List<List<Stock>> copyAllStockPriceData = allStockPriceData.Select(list =>
            {
                return new List<Stock>(list);
            }).ToList();


            // For each day 
            while (finishCount != allStockPriceData.Count)
            {
                double subCost = 0;
                double subValue = 0;

                finishCount = 0; // reset the counter 

                bool marketOpen = false; 

                // for each sybmol
                for (int i = 0; i < copyAllStockPriceData.Count; i++)
                {
                    if (copyAllStockPriceData[i].Count() <= 0)
                    {
                        // increment as there are no more stock prices in this list so it is "finished" 
                        finishCount++;
                    } else
                    {
                        Stock stockData = copyAllStockPriceData[i].First();

                        if (dateIndex == stockData.date)
                        {
                            marketOpen = true; 
                            copyAllStockPriceData[i].RemoveAt(0); // poll the element 

                            subCost += stockData.sharesBought * stockData.price;

                            if (reinvestDividends)
                            {
                                totalValueOfSharesFromDividends = (stockData.sharesBoughtWithDividend * stockData.price);
                                subValue += (stockData.sharesOwned * stockData.price) + totalValueOfSharesFromDividends;
                            } else
                            {
                                dividendsEarned += stockData.dividend * stockData.sharesOwned;
                                subValue += (stockData.sharesOwned * stockData.price) + dividendsEarned;
                            }

                        }
                    }   
                }

                totalCost += subCost;

                // value of shares owned today 
                double temp = subValue; 
                subValue = temp - totalValue;
                totalValue = temp;

                if (marketOpen)
                {
                    networthDataPoints.Add(new NetWorthDataPoint
                    {
                        date = dateIndex,
                        totalValue = totalValue, 
                        totalCost = totalCost, 
                        subValue = subValue,
                        subCost = subCost, 
                        differenceFromCost = totalValue - totalCost, 
                        dividendEarned = dividendsEarned,
                        valueOfSharesFromDividend = totalValueOfSharesFromDividends,
                        note = "not implemented yet"
                    });
                }

                dateIndex = dateIndex.AddDays(1);
            }

            return networthDataPoints;
        }


        [HttpPost]
        public IActionResult Calculate(string inputData, DateTime targetDate, bool moneyFormat, bool reinvestDividends)
        {

            var inputDataList = JsonConvert.DeserializeObject<List<Stock>>(inputData);

            var uniqueSymbols = getUniqueSymbols(inputDataList);

            IList<IList<Stock>> allStockPriceData = new List<IList<Stock>>();

            foreach (string symbol in uniqueSymbols.Keys)
            {
                allStockPriceData.Add(getPriceDataAndCombineWithInput(symbol, inputDataList, uniqueSymbols, moneyFormat, reinvestDividends).ToList());
            }

            IEnumerable<NetWorthDataPoint> networthDataPoints = getNetworthDataPoints(allStockPriceData, reinvestDividends);

            PortfolioResult result = new PortfolioResult
            {
                allStockPriceData = allStockPriceData,
                networthDataPoints = networthDataPoints
            };

            return Json(new
            {
                result = JsonConvert.SerializeObject(result)
            }); 
        }

        // CSB992QM4L7LLVY5

        public IEnumerable<JToken> getAVStockInfo(string symbol)
        {     
            const string URL = "https://www.alphavantage.co/query";
            string urlParameters = "?function=TIME_SERIES_DAILY_ADJUSTED&apikey=CSB992QM4L7LLVY5&outputsize=full&symbol=" + symbol;

            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(URL);

            // Add an Accept header for JSON format.
            client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response = client.GetAsync(urlParameters).Result;  // Blocking call! Program will wait here until a response is received or a timeout occurs.
            if (response.IsSuccessStatusCode)
            {
                // Parse the response body.
                var dataObjects = response.Content.ReadAsAsync<JObject>().Result;  //Make sure to add a reference to System.Net.Http.Formatting.dll
                var dataArr = dataObjects["Time Series (Daily)"].ToArray();
            
                return dataArr;
            }
            else
            {
                Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                return null;
            }
        }


        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
