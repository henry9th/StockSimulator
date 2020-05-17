function addNewRow() {
    var stockInputsDiv = document.getElementById("stock-inputs");
    var numSharesDiv = document.getElementById("numShares-inputs");
    var dateDiv = document.getElementById("date-inputs");

    var stockInput = document.createElement("input");
    stockInput.setAttribute("type", "text");

    var numSharesInput = document.createElement("input");
    numSharesInput.setAttribute("type", "number");
    numSharesInput.setAttribute("min", "0");

    var dateInput = document.createElement("input");
    dateInput.setAttribute("type", "date");

    stockInputsDiv.appendChild(stockInput);
    numSharesDiv.appendChild(numSharesInput);
    dateDiv.appendChild(dateInput);
}


function calculatePortfolio() {
    var stockInputs = $("#stock-inputs input");
    var numSharesInputs = $("#numShares-inputs input");
    var dateInputs = $("#date-inputs input");

    var targetDate = document.getElementById("result-date").value;

    var count = stockInputs.length;

    let inputData = [];

    // Organize each row data into each dict
    for (i = 0; i < count; i++) {
        var symbol = stockInputs[i].value;
        var numShares = numSharesInputs[i].value;
        var date = dateInputs[i].value;

        console.log(symbol);
        console.log(numShares);
        console.log(date);

        if (symbol == "" || isNaN(numShares) || numShares < 0 || date == "") {
            alert("Unallowed value on row: " + (i + 1));
            return;
        }

        inputData.push({
            symbol: symbol,
            numShares: numShares,
            date: date
        });

    }
    console.log("inputData:", inputData);

    // Get a list of unique symbols along with date (for minimizing dividend and current price requests)
    // (Symbol, Date)
    var uniqueSymbols = new Map();
    for (i = 0; i < inputData.length; i++) {

        var sym = inputData[i].symbol;
        var date = inputData[i].date;

        if (uniqueSymbols.has(sym)) {
            var oldDate = uniqueSymbols.get(sym);

            // we want to find the oldest date for each
            if (date < oldDate) {
                uniqueSymbols.set(sym, date);
            }
        } else {
            uniqueSymbols.set(sym, date);
        }
    }

    console.log("map of unique values:", uniqueSymbols);

    // get the prices of stock on purchase date
    var purchasePrices = [];
    for (i = 0; i < inputData.length; i++) {
        var data = inputData[i];
        var priceData = getPriceData(data.symbol, data.date, data.numShares);

        if (priceData == null) {
            return;
        }

        purchasePrices.push(getPriceData(data.symbol, data.date, data.numShares));
    }

    // get the prices of stock on target date
    // (Symbol, Data)
    var currentPrices = new Map();
    for (var uniqueValue of uniqueSymbols) {
        currentPrices.set(sym, getPriceData(uniqueValue[0], targetDate));
    }

    console.log("current prices map: ", currentPrices);

    // calculate total cost of purchasing and total value as of target date
    var totalCost = 0;
    var totalValue = 0;
    for (i = 0; i < purchasePrices.length; i++) {
        var data = purchasePrices[i];
        totalCost += (data.numShares * data.price);

        totalValue += (data.numShares * currentPrices.get(data.symbol).price);
    }

    document.getElementById("total-cost-text").innerHTML = "Total Cost: " + totalCost.toFixed(2);
    document.getElementById("total-value-text").innerHTML = "Total Value as of " + targetDate + ": " + totalValue.toFixed(2);

}


var iex_api_key = "pk_dade0ec68b064458acfe1288bc8924df";

function getPriceData(symbol, date, numShares) {

    var response = [];
    var retryCount = 0;

    // loop in case provided date was not a market day
    while (response.length == 0) {

        // when there is no data for the provided day or symbol
        if (retryCount == 5) {
            return;
        }

        formattedDate = date.replace(/-/g, '');

        var historicalUrl;

        // Special case is when we are fetching the most current data (date==today), then we should get the last 2 points
        // Getting just the most recent sometimes returns nulls for prices
        // check if the target date provided is today
        if (date == (new Date()).toISOString().substr(0, 10)) {
            // GET /stock/{symbol}/chart/{range}/{date}
            historicalUrl = "https://cloud.iexapis.com/stable/stock/" + symbol + "/chart/date/" + formattedDate + "?chartLast=1&token=" + iex_api_key;
        } else {
            var historicalUrl = "https://cloud.iexapis.com/stable/stock/" + symbol + "/chart/date/" + formattedDate + "?chartLast=2&token=" + iex_api_key;
        }

        console.log(historicalUrl);

        var xmlHttp = new XMLHttpRequest();
        xmlHttp.open("GET", historicalUrl, false); // false for synchronous request
        xmlHttp.send(null);

        var response = xmlHttp.responseText;
        console.log("response: ", response);

        // check if the symbol was not detectd
        try {
            response = JSON.parse(xmlHttp.responseText);
            if (response.length == 0) {
                var dateObj = new Date(date);

                dateObj.setDate(dateObj.getDate() - 1); // go back a day
                date = dateObj.getFullYear() + '-' +
                    ('0' + (dateObj.getMonth() + 1)).slice(-2) + '-' +
                    ('0' + dateObj.getDate()).slice(-2); // convert date to ISO string
            }

            var fullData = {
                price: response[0].close, // use Closing prices
                symbol: symbol,
                date: date,
                minute: response[0].minute,
                numShares: numShares
            }

            console.log(fullData);

            return fullData;

        } catch (err) {
            alert("The symbol " + symbol + " could not be found");
            return null;
        }
    }
}