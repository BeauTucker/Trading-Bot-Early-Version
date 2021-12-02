//AFET BETA (Version 1.2.0)

using Alpaca.Markets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HtmlAgilityPack;

namespace AlgoTrader
{
    class Program
    {
        //allows API Access
        private string API_KEY = "";
        private string API_SECRET = "";

        //declaring a variable to hold the picked stock
        public string pickedStock;

        //declaring variables that can have decimal values
        decimal totalAccountVal;
        decimal usableAccountVal;
        decimal pickedStockPrice;
        //decimal dayAccountChange;
        decimal maxPriceInTrade;
        decimal sellPrice;
        decimal buyPrice;
        decimal soldAt;
        decimal weightDifference;
        decimal weightMultiplier = 2;


        //declaring solely whole number variables
        int initialShareSize;
        public int tradesTaken;

        //declaring boolean value holders
        public bool inTrade;
        public bool active = false;
        public bool stockFound;
        public bool dataObtained;
        public bool enoughtMoney;
        public bool awaitingData;
        public bool tradingHours = false;
        public bool pricesGot = false;
        public bool stockReady = false;
        public bool hoursGood = false;
        public bool minutesGood = false;
        public bool stockFoundStop = false;
        public bool dataObtainedStop = false;
        public bool pendingBuy = false;
        public bool pendingSell = false;
        public bool marketOpenSaid = false;
        public bool alreadyQuit = false;
        public bool saidAwatingTrades = false;
        public bool saidMarketClosed = false;
        public bool hasBeenOpenOnInstance = false;

        //declaring API based variables to call later
        private IAlpacaDataClient alpacaDataClient;
        private IAlpacaTradingClient alpacaTradingClient;
        private IAlpacaStreamingClient alpacaStreamingClient;
        private IAlpacaDataStreamingClient alpacaDataStreamingClient;

        List<decimal> quoteCollection = new List<decimal>();
        List<string> tradedStocks = new List<string>();

        decimal minuteAvg;

        //called every second to maintain updated prices and responses
        public void Update(object o)
        {
            DetermineMarketOpen();
            if ((marketOpenSaid == false) && (tradingHours == true))
            {
                marketOpenSaid = true;
                saidMarketClosed = false;
                tradesTaken = 0;
                tradedStocks.Clear();
                hasBeenOpenOnInstance = true;
                Console.WriteLine("Market Open...");
            }

            if ((active == true) && (tradingHours == true))
            {
                UseStrategy();

                if (stockFound == true)
                {
                    GetPrices();
                }

                if ((stockFound == false) && (dataObtainedStop == false))
                {
                    dataObtainedStop = true;
                    Console.WriteLine("Finding Security...");
                    FindStock();
                }
                else if ((dataObtained == false) && (stockFound == true) && (pricesGot == true) && (stockFoundStop == false))
                {
                    stockFoundStop = true;
                    Console.WriteLine("Retrieving Data...");
                    GetData();

                }

                if ((inTrade == false) && (stockFound == true) && (dataObtained == true) && (quoteCollection.Count >= 39))
                {
                    DetermineStockReady();
                    if(saidAwatingTrades == false)
                    {
                        Console.WriteLine("Awaiting Trades... ");
                        Console.WriteLine("");
                        saidAwatingTrades = true;
                    }
                }
            }
            if((tradingHours == false) && (inTrade == true))
            {
                Sell();
            }

            if (tradingHours == false)
            {
                marketOpenSaid = false;

                if(saidMarketClosed == false && hasBeenOpenOnInstance == true)
                {
                    Console.WriteLine("Market Closed...");
                    Console.WriteLine("Trades Taken Today: " + tradesTaken);
                    Console.WriteLine("Stocks Traded Today: " + tradedStocks);
                    Console.WriteLine("");
                    Console.WriteLine("Awaiting Market Open...");
                    saidMarketClosed = true;
                }
            }
        }
       
        //holds trade logic
        public void UseStrategy()
        {
            if ((stockFound == true) && (pendingBuy == false))
            {
                if ((stockReady == true) && (inTrade == false) && (dataObtained == true))
                {
                    pendingBuy = true;
                    buyPrice = pickedStockPrice;
                    Buy();
                }
            }

            else if ((inTrade == true) && (pendingSell == false))
            {
                if (pickedStockPrice <= sellPrice)
                {
                    soldAt = pickedStockPrice;
                    pendingSell = true;
                    Sell();
                }
            }
        }

        //finds stock based on parameters
        public async void FindStock()
        {
            alpacaDataClient = Environments.Paper.GetAlpacaDataClient(new SecretKey(API_KEY, API_SECRET));

            pickedStock = GetHTML();
            Console.WriteLine("Security Chosen: " + pickedStock);

            bool listContainsAlready = tradedStocks.Contains(pickedStock);
            if (listContainsAlready == false)
            {
                tradedStocks.Add(pickedStock);
            }

            var last = await alpacaDataClient.GetLatestQuoteAsync(pickedStock);

            pickedStockPrice = last.BidPrice;
            stockFound = true;
            
            Console.WriteLine(pickedStock + " is currently trading at $" + pickedStockPrice);
            Console.WriteLine("");
        }

        //called every 5 minutes to check for picked stock relevance
        public void RefreshStock(object o)
        {
            if (stockFound == true)
            {
                string reference = GetHTML();
                if ((pickedStock != reference) && (inTrade == false))
                {
                    Reset();
                }
            }
        }

        //retrieves stock ticker from gap scanner
        public string GetHTML()
        {
            string url = "https://www.webull.com/quote/rankgainer";
            var web = new HtmlWeb();
            var doc = web.Load(url);
            var tag = doc.DocumentNode.SelectSingleNode("//*[@id='app']/section/section[2]/section/section[2]/div[2]/div[1]/a");
            var sto = tag.InnerText;
            
            return sto;
        }

        //buys stock at calculated position size
        public async void Buy()
        {
            maxPriceInTrade = 0;

            stockReady = false;
            Console.WriteLine("Bought " + initialShareSize + " shares of " + pickedStock + " at $" + buyPrice);
            Console.WriteLine("");

            await alpacaTradingClient.PostOrderAsync(OrderSide.Buy.Market(pickedStock, initialShareSize));

            inTrade = true;
            pendingSell = false;
        }

        //sells exactly what was bought
        public async void Sell()
        {
            Console.WriteLine("Sold " + initialShareSize + " shares of " + pickedStock + " at $" + soldAt);
            Console.WriteLine("");

            await alpacaTradingClient.DeleteAllPositionsAsync();

            Thread.Sleep(5000);

            inTrade = false;
            stockFound = false;
            dataObtainedStop = false;
            dataObtained = false;
            stockFoundStop = false;
            pendingBuy = false;
            pendingSell = false;
            saidAwatingTrades = false;
            pricesGot = false;
            stockReady = false;
            tradesTaken = tradesTaken + 1;
            quoteCollection.Clear();
        }

        public void Reset()
        {
            inTrade = false;
            stockFound = false;
            dataObtainedStop = false;
            dataObtained = false;
            stockFoundStop = false;
            pendingBuy = false;
            pendingSell = false;
            saidAwatingTrades = false;
            pricesGot = false;
            stockReady = false;
            quoteCollection.Clear();
        }

        //calculates position size based on account value and stock price... called right before trade entrance
        public async void GetData()
        {
            alpacaTradingClient = Environments.Paper.GetAlpacaTradingClient(new SecretKey(API_KEY, API_SECRET));

            var account = await alpacaTradingClient.GetAccountAsync();

            totalAccountVal = Convert.ToDecimal(account.Equity);
            usableAccountVal = 0.9m * totalAccountVal;
            initialShareSize = Convert.ToInt32(Math.Floor(usableAccountVal / pickedStockPrice));

            Console.WriteLine("Equity: $" + totalAccountVal);
            Console.WriteLine("Usable Equity: $" + usableAccountVal);
            Console.WriteLine("Planned position size for " + pickedStock + " is " + initialShareSize + " shares.");
            Console.WriteLine("");

            dataObtained = true;
        }

        //calculates a price to sell at upon order entry
        public async void GetPrices()
        {
            alpacaDataClient = Environments.Paper.GetAlpacaDataClient(new SecretKey(API_KEY, API_SECRET));

            var last = await alpacaDataClient.GetLatestQuoteAsync(pickedStock);
            
            pickedStockPrice = last.AskPrice;

            quoteCollection.Insert(0, pickedStockPrice);

            if (quoteCollection.Count > 40)
            {
                quoteCollection.RemoveAt(quoteCollection.Count - 1);
            }

            if (quoteCollection.Count == 2)
            {
                Console.WriteLine("Warming Up Indicators...");
                Console.WriteLine("");
            }

            if (quoteCollection.Count == 21)
            {
                Console.WriteLine("Indicators Half Warm...");
                Console.WriteLine("");
            }

            if (quoteCollection.Count == 34)
            {
                Console.WriteLine("Indicators Ready In: ");
            }

            if (quoteCollection.Count == 35)
            {
                Console.WriteLine("5");
            }

            if (quoteCollection.Count == 36)
            {
                Console.WriteLine("4");
            }

            if (quoteCollection.Count == 37)
            {
                Console.WriteLine("3");
            }

            if (quoteCollection.Count == 38)
            {
                Console.WriteLine("2");
            }

            if (quoteCollection.Count == 39)
            {
                Console.WriteLine("1");
                Console.WriteLine("");
            }

            if (pickedStockPrice != 0)
            {
                pricesGot = true;
            }

            maxPriceInTrade = Math.Max(pickedStockPrice, maxPriceInTrade);

            weightDifference = maxPriceInTrade - buyPrice;

            if (weightDifference < 0)
            {
                weightDifference = 0;
            }
            
            sellPrice = maxPriceInTrade - (maxPriceInTrade * (0.012m / (1 + (weightDifference * weightMultiplier))));
        }

        //determines if a stock fits the strategy's criteria to be bought
        public void DetermineStockReady()
        {
            minuteAvg = quoteCollection.Average();

            if (pickedStockPrice > (1.011m * minuteAvg))
            {
                stockReady = true;
            }
            else
            {
                stockReady = false;
            }
        }

        //determines if the market is open and keeps orders from going through in non-trading hours
        public void DetermineMarketOpen()
        {
            var minutes = DateTime.Now.TimeOfDay.Minutes;
            var hours = DateTime.Now.TimeOfDay.Hours;
            var day = DateTime.Now.DayOfWeek;

            if ((day != DayOfWeek.Sunday) && (day != DayOfWeek.Saturday))
            {
                if ((hours >= 8) && (hours < 15))
                {
                    hoursGood = true;
                }
                else
                {
                    hoursGood = false;
                }

                if ((hours >= 8) && (hours < 9))
                {
                    if (minutes >= 31)
                    {
                        minutesGood = true;
                    }
                    else
                    {
                        minutesGood = false;
                    }
                }

                if ((hours >= 14) && (hours < 15))
                {
                    if (minutes < 59)
                    {
                        minutesGood = true;
                    }
                    else
                    {
                        minutesGood = false;
                    }
                }

                if ((hours >= 9) && (hours < 14))
                {
                    minutesGood = true;
                }

                if ((hoursGood == true) && (minutesGood == true))
                {
                    tradingHours = true;
                }
                else
                {
                    tradingHours = false;
                }
            }

            else
            {
                tradingHours = false;
            }
        }

        //Allows the user to interact with the program and control activation and traded stock
        public void PromptActivation ()
        {
            alreadyQuit = false;
            Console.WriteLine("");

            Console.WriteLine("Enter your Alpaca API key below");
            API_KEY = Console.ReadLine();
            Console.WriteLine("\nEnter your Alpaca API secret key below.");
            API_SECRET = Console.ReadLine();
            Console.WriteLine("\nPress ENTER to activate AFET...");
            string input = Console.ReadLine();
            active = true;
        }

        //provides information to user
        public void UserInterface()
        {
            if (tradingHours == false)
            {
                Console.WriteLine("Awaiting market open...");
                Console.WriteLine("");
                Console.WriteLine("Market Hours:");
                Console.WriteLine("09:30 - 16:00 (EST)");
                Console.WriteLine("08:30 - 15:00 (CST)");
                Console.WriteLine("07:30 - 14:00 (MST)");
                Console.WriteLine("06:30 - 13:00 (PST)");
                Console.WriteLine("");
            }
            else
            {
                Console.WriteLine("Market Open...");
                marketOpenSaid = true;
            }
        }

        //ensures there are no active trades upon deactivation
        public void Quit()
        {
            if (inTrade == true)
            {
                Sell();
                inTrade = false;
            }
            Console.WriteLine("");
            Console.WriteLine("AFET Status: Inactive");
            Console.WriteLine("--------------------------------------------");
            Console.WriteLine("");
            active = false;
        }
    }

    class MainFunc
    {
        //calls update every second, update calls everything else
        public static void Main()
        {
            var p = new Program();

            p.stockFound = false;
            p.dataObtained = false;
            Console.WriteLine("Developed by Machina Capital Management, LLC");

            p.PromptActivation();
            Console.WriteLine("AFET Status: Active");
            Console.WriteLine("Press ENTER to Deactivate");
            Console.WriteLine("");
            p.UserInterface();

            var timer = new Timer(p.Update, null, 0, 1500);
            var timer1 = new Timer(p.RefreshStock, null, 0, 300000);

            Console.ReadLine();
            p.Quit();
            Main();   
        }
    }
}
