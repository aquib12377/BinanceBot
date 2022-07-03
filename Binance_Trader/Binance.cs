using System;
using System.Collections.Generic;
using Binance.Net;
using Binance.Net.Objects;
using Binance.Net.Enums;
using CryptoExchange.Net.Authentication;
using System.Threading.Tasks;
using System.IO;
using System.Numerics;
using Binance.Net.Interfaces;
using System.Linq;
using Skender.Stock.Indicators;
using System.Drawing;
using CryptoExchange.Net.Objects;
using Binance.Net.Clients;
using CryptoExchange.Net.CommonObjects;
using System.Text;
using System.Collections;

namespace Binance_Trader
{
    public class Binance
    {
        private static readonly double UpperT = 70;
        private static readonly double LowerT = -70;
        private static readonly double ZeroLine = 0;
        #region Signal variables
        private static readonly string _signalFile = "BUY_SELL.txt";
        private static string _previousSignal = "";
        #endregion
        #region Indicators variables
        //CCI 
        private List<CciResult> Cci;
        //RSI
        private List<RsiResult> Rsi;
        //MA
        private List<EmaResult> Ema;
        private List<ParabolicSarResult> ParabolicSarResults;
        #endregion
        #region My Variables
        private bool? prevSS;
        private bool? prevLL;
        private int Precision = 0;
        private bool IsFirsTime = true;
        private List<Quote> Quotes;
        private WebCallResult<IEnumerable<IBinanceKline>> HistoricalData;
        private DateTime TodaysDate = DateTime.Now;
        private DateTime StartTime = DateTime.Now;
        private DateTime KlineTime = DateTime.Now;

        private List<IBinanceKline> Klines;
        
        public bool DebugMode = false;
        private string _coin = string.Empty;
        private BinanceClient Client;
        private Logger Logger;
        #endregion
        public async Task Initialize(string coinName, int precision = 3, bool isDebugMode = true)   
        {
            Logger = new Logger();
            try
            {
                #region Initializing Binance Clients for API interaction
                //BinanceSocket = new BinanceSocketClient(new BinanceSocketClientOptions()
                //{
                //    ApiCredentials = new ApiCredentials("caJ02pCPrdQTrStLR9YnpVwNGgxEU6JV0s8pHCnEeEHVhAOuk5PpfkDLFLYkEpsD"
                //    , "Sttc66kCjvfHfSOcp98ON8DLoub4kgS9wcLJcUk49wfVfpDbipXAbY0W7Zx4iYzW")
                //});
                Client = new BinanceClient(new BinanceClientOptions()
                {
                    ApiCredentials = new ApiCredentials("1jyeTlSyHIjqnRpwIfipPZgqKS39BXuVzTRNdxojQ7t8zyAnYwRd5E8ksT2JlQcF"
                        , "tr9t0BbbpF4AAURh58CC13IfDhoBGsjcjo9TBGiKhP5zxhBTMa2vZSf7o2XYyNfw")
                });
                #endregion
                #region Validating API
                var accountInfo = await Client.UsdFuturesApi.CommonFuturesClient.GetBalancesAsync();
                if (accountInfo.Success != true && accountInfo.ResponseStatusCode != System.Net.HttpStatusCode.OK)
                {
                    Logger.Log("Something went wrong. Please check the API restrictions in binance and try again.");
                    Environment.Exit(-1);
                }
                Logger.Log("Account connected Successfully.\r\nFutures USDT blance: " + accountInfo.Data.FirstOrDefault(x => x.Asset == "USDT").Available.ToString());
                #endregion
                #region Initializing private variables for executing trades and collecting previous data
                _coin = coinName;
                Precision = precision;
                Klines = new List<IBinanceKline>();
                Quotes = new List<Quote>();
                StartTime = new DateTime(2020, 8, 22, 0, 0, 0);
                var TempDate = DateTime.Now;
                TodaysDate = new DateTime(TempDate.Year, TempDate.Month, TempDate.Day, TempDate.Hour, TempDate.Minute, 0);
                #endregion
            }
            catch (Exception _)
            {
                string error = string.Format("[{0}] Error while connecting account : {1} \r\n Stacktrace: {2}",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    _.Message ?? _.InnerException.ToString(), _.StackTrace);
                Logger.Log(error);
            }

        }
        public async Task BackTestStrategies()
        {
            var f = await File.ReadAllLinesAsync(@"C:\Users\Mohammed Aquib Ansar\source\repos\Binance_HistoricalDataSaver\bin\Debug\netcoreapp3.1\ICPUSDT.txt");
            var file = f.Select(x => x.Split(",")).ToList();

            for (int i = 1; i < file.Count - 1; i++)
            {
                Quotes.Add(new Quote
                {
                    Date = DateTime.Parse(file[i][0]),
                    Open = Convert.ToDecimal(file[i][1]),
                    High = Convert.ToDecimal(file[i][2]),
                    Low = Convert.ToDecimal(file[i][3]),
                    Close = Convert.ToDecimal(file[i][4]),
                    Volume = Convert.ToDecimal(file[i][5])
                });
            }

            CalculateIndicators();
            TestSignal();
        }
        private void UpdateQuote(IEnumerable<IBinanceKline> klines)
        {
            try
            {
                Logger.Log("Updating Quotes List");
                foreach (var kline in klines)
                {
                    Quotes.Add(new Quote
                    {
                        Date = TimeZoneInfo.ConvertTimeFromUtc(kline.CloseTime, TimeZoneInfo.Local),
                        Open = kline.OpenPrice,
                        High = kline.HighPrice,
                        Low = kline.LowPrice,
                        Close = kline.ClosePrice,
                        Volume = kline.QuoteVolume,
                    });
                }
                Logger.LogSuccess($"Successfully Updatdet Quotes List. Added {klines.Count()} quotes.");
            }
            catch (Exception _)
            {
                Logger.LogError(_);
            }

        }
        public async Task PlaceOrder(bool _long, string coin, decimal percentage = 0.0m)
        {
            #region fetching balance and coin price
            var usdtb = await Client.UsdFuturesApi.Account.GetBalancesAsync();
            var usdtBalance = usdtb.Data.Where(x => x.Asset == "USDT").FirstOrDefault().AvailableBalance;
            var cp = await Client.UsdFuturesApi.ExchangeData.GetMarkPriceAsync(coin);
            var coinPrice = cp.Data.MarkPrice;
            #endregion
            #region Calculating position size based on 15x Leverage
            var positionSize = usdtBalance * 19;
            #endregion
            #region Placing  order with TP 5% and SL 5%

            var op = await Client.UsdFuturesApi.Trading.GetOpenOrdersAsync(coin);

            Logger.Log($"There are {op.Data.Count()} open orders.");

            foreach (var order in op.Data)
            {
                var cancelResponse = await Client.UsdFuturesApi.Trading.CancelOrderAsync(coin, order.Id);
            }

            try
            {
                if (_long)
                {
                    var lastCandleLow = Enumerable.Reverse(Quotes).Take(5).Reverse().Max(x => x.Low);
                    
                    coinPrice = Math.Round(coinPrice, Precision);
                    positionSize = Math.Round(positionSize, Precision);
                    var quantity = Math.Round(positionSize / coinPrice, Precision);
                    var stopLossPrice = Math.Round(lastCandleLow, Precision);
                    if (stopLossPrice == coinPrice)
                        stopLossPrice = Math.Round(coinPrice - ((0.5m / 100) * coinPrice),Precision);
                    var takeProfitPrice = Math.Round(((0.4m / 100) * coinPrice) + coinPrice, Precision);
                    var openPositionResult = await Client.UsdFuturesApi.Trading.PlaceOrderAsync(coin, OrderSide.Buy, FuturesOrderType.Market, quantity);
                    //Console.WriteLine(openPositionResult.Error.Message ?? "");
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine(string.Format("[{0}] [Status - {3}] Opened LONG Position - Price: {1} PositionSize: {2}",
                        DateTime.Now, coinPrice, positionSize, openPositionResult.ResponseStatusCode));
                    Console.ResetColor();
                    var stopLossResult = await Client.UsdFuturesApi.Trading.PlaceOrderAsync(coin, OrderSide.Sell, FuturesOrderType.StopMarket, quantity: null, closePosition: true, stopPrice: stopLossPrice);
                    //Console.WriteLine(stopLossResult.Error.Message??"");
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine(string.Format("[{0}] [Status - {2}] Placed SL for above LONG Position - SLPrice: {1}",
                        DateTime.Now, stopLossPrice, stopLossResult.ResponseStatusCode));
                    Console.ResetColor();
                    var takeProfitResult = await Client.UsdFuturesApi.Trading.PlaceOrderAsync(coin, OrderSide.Sell, FuturesOrderType.TakeProfitMarket, quantity: null, closePosition: true, stopPrice: takeProfitPrice);
                    //Console.WriteLine(takeProfitResult.Error.Message ?? "");
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine(string.Format("[{0}] [Status - {2}] Placed TP for above LONG Position - TPPrice: {1}",
                        DateTime.Now, takeProfitPrice, takeProfitResult.ResponseStatusCode));
                    Console.ResetColor();
                }
                else
                {
                    var lastCandleHigh = Enumerable.Reverse(Quotes).Take(5).Reverse().Max(x => x.High);
                    coinPrice = Math.Round(coinPrice, Precision);
                    positionSize = Math.Round(positionSize, Precision);
                    var quantity = Math.Round(positionSize / coinPrice, Precision);
                    var stopLossPrice = Math.Round(lastCandleHigh, Precision);
                    if (stopLossPrice == coinPrice)
                        stopLossPrice = Math.Round(coinPrice + ((0.5m / 100) * coinPrice),Precision);
                    var takeProfitPrice = Math.Round(coinPrice - ((0.4m / 100) * coinPrice), Precision);
                    var openPositionResult = await Client.UsdFuturesApi.Trading.PlaceOrderAsync(coin, OrderSide.Sell, FuturesOrderType.Market, quantity);
                    //Console.WriteLine(openPositionResult.Error.Message ?? "");
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine(string.Format("[{0}] [Status - {3}] Opened SHORT Position - Price: {1} PositionSize: {2}",
                        DateTime.Now, coinPrice, positionSize, openPositionResult.ResponseStatusCode));
                    Console.ResetColor();
                    var stopLossResult = await Client.UsdFuturesApi.Trading.PlaceOrderAsync(coin, OrderSide.Buy, FuturesOrderType.StopMarket, quantity: null, closePosition: true, stopPrice: stopLossPrice);
                    //Console.WriteLine(stopLossResult.Error.Message??"");
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine(string.Format("[{0}] [Status - {2}] Placed SL for above SHORT Position - SLPrice: {1}",
                        DateTime.Now, stopLossPrice, stopLossResult.ResponseStatusCode));
                    Console.ResetColor();
                    var takeProfitResult = await Client.UsdFuturesApi.Trading.PlaceOrderAsync(coin, OrderSide.Buy, FuturesOrderType.TakeProfitMarket, quantity: null, closePosition: true, stopPrice: takeProfitPrice);
                    //Console.WriteLine(takeProfitResult.Error.Message ?? "");
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine(string.Format("[{0}] [Status - {2}] Placed TP for above SHORT Position - TPPrice: {1}",
                        DateTime.Now, takeProfitPrice, takeProfitResult.ResponseStatusCode));
                    Console.ResetColor();
                }
            }
            catch (Exception e)
            {
                Logger.Log(e.Message ?? e.InnerException.ToString() + "\r\n\r\n" + e.StackTrace);
            }

            #endregion
        }
        public async Task CollectDataset()
        {
            try
            {
                if (IsFirsTime)
                {
                    Logger.Log($"Fetching Klines from Binance");
                    ConsoleSpiner spin = new ConsoleSpiner();
                    while (TodaysDate > StartTime)
                    {
                        var endTime = StartTime.AddHours(96);
                        HistoricalData = await Client.UsdFuturesApi.ExchangeData
                            .GetContinuousContractKlinesAsync(_coin, ContractType.Perpetual, KlineInterval.FiveMinutes, StartTime, endTime);
                        if (HistoricalData.Data != null)
                        {
                            if (!HistoricalData.Data.Any() && IsFirsTime)
                            {
                                StartTime = StartTime.AddHours(24);
                                continue;
                            }
                            if (HistoricalData.Data.Count() == 0 && Klines.Count != 0 && StartTime > TodaysDate)
                                break;
                            Klines.AddRange(HistoricalData.Data);
                            StartTime = Klines.LastOrDefault().CloseTime;
                            Logger.Log($"Fetched {HistoricalData.Data.Count()} Klines from Binance. Till {StartTime}.");
                        }
                        else
                            Logger.Log("Response for previous data is null");
                        spin.Turn();
                    }

                    Logger.LogSuccess($"Successfully fetched and saved {Klines.Count} Klines.");
                    UpdateQuote(Klines);

                    Quotes = Quotes.Distinct().ToList();

                    KlineTime = Klines.LastOrDefault().CloseTime;
                    CalculateIndicators();
                    var x = Quotes.LastOrDefault();
                    await GenerateSignal();
                    IsFirsTime = false;
                }
                else
                {
                    HistoricalData = await Client.UsdFuturesApi.ExchangeData.GetContinuousContractKlinesAsync(_coin, ContractType.Perpetual, KlineInterval.FiveMinutes, KlineTime);
                    if (HistoricalData.Data != null)
                        if (HistoricalData.Data.Any())
                        {
                            UpdateQuote(HistoricalData.Data);

                            Quotes = Quotes.Distinct().ToList();
                            Klines.AddRange(HistoricalData.Data);
                            Logger.LogSuccess($"Updated Klines.\r\n Added {HistoricalData.Data.Count()} Klines to Dataset");
                            KlineTime = Klines.LastOrDefault().CloseTime;
                            CalculateIndicators();
                            await GenerateSignal();
                        }
                        else { }
                }
            }
            catch (Exception _)
            {
                Logger.LogError(_);
            }
        }
        private async Task GenerateSignal()
        {
            try
            {
                #region Generate signal
                var quote = Quotes.LastOrDefault();
                var sar = ParabolicSarResults[^1];
                if (sar.IsReversal == false || sar.IsReversal == null) return;
                var openC = Convert.ToDecimal(sar.Sar.Value) > quote.Open;
                var closeC = Convert.ToDecimal(sar.Sar.Value) > quote.Close;
                var highC = Convert.ToDecimal(sar.Sar.Value) > quote.High;
                var lowC = Convert.ToDecimal(sar.Sar.Value) > quote.Low;

                var openS = Convert.ToDecimal(sar.Sar.Value) < quote.Open;
                var closeS = Convert.ToDecimal(sar.Sar.Value) < quote.Close;
                var highS = Convert.ToDecimal(sar.Sar.Value) < quote.High;
                var lowS = Convert.ToDecimal(sar.Sar.Value) < quote.Low;
                var _long =  openC && closeC && highC && lowC;
                var _short = openS && closeS && highS && lowS;

                #endregion
                #region Placing order based on signal
                if (_long && (prevLL == null || prevLL != true) && IsFirsTime == false)
                {
                    await PlaceOrder(true, _coin, 80m);
                    prevLL = true;
                    prevSS = false;
                    //Logger.LogSignal(true, false, quote.Close, $"Buy - Long {quote.Date}");
                    File.AppendAllText(_signalFile, string.Format("{0},{1},{2},{3}\r\n",
                                                    quote.Date, quote.Low, quote.High, "LONG"));
                }
                else if (_short && (prevSS == null || prevSS != true) && IsFirsTime == false)
                {
                    await PlaceOrder(false, _coin, 80m);
                    prevSS = true;
                    prevLL = false;
                    //Logger.LogSignal(true, false, quote.Close, $"Buy - SHORT {quote.Date}");
                    File.AppendAllText(_signalFile, string.Format("{0},{1},{2},{3}\r\n",
                                                    quote.Date, quote.Low, quote.High, "SHORT"));
                }
                else
                {
                    Logger.LogSignal(true, false, quote.Low, $"NOBUY {quote.Date}");
                }
                IsFirsTime = false;
                #endregion
            }
            catch (Exception _)
            {
                Logger.LogError(_);
            }
        }
      
        private void TestSignal()
        {
            #region Trend Filter
            StringBuilder builder = new StringBuilder();
            builder.Append("Date,ClosePrice,OpenPrice,Signal\r\n");
            bool? prevLLT = null;
            bool? prevSST = null;
            for (int i = 2; i < Quotes.Count - 1; i++)
            {
                if (Cci[i].Cci.Value == 0)
                    continue;

                var _longT = false;
                var _shortT = false;
                var quoteT = Quotes[i];


                string line;
                if (_longT && _shortT)
                {
                    line = string.Format("{0},{1},{2},{3}\r\n",
                        quoteT.Date, quoteT.Low, quoteT.High, "INVALID TRADE");
                    builder.Append(line);
                }
                else if (_longT && (prevLLT == null || prevLLT != true))
                {
                    prevLLT = true;
                    prevSST = false;
                    line = string.Format("{0},{1},{2},{3}\r\n",
                        quoteT.Date, quoteT.Low, quoteT.High, "LONG");
                    builder.Append(line);
                }
                else if (_shortT && (prevSST == null || prevSST != true))
                {
                    prevSST = true;
                    prevLLT = false;
                    line = string.Format("{0},{1},{2},{3}\r\n",
                        quoteT.Date, quoteT.Low, quoteT.High, "SHORT");
                    builder.Append(line);
                }
                else
                {
                    line = string.Format("{0},{1},{2},{3}\r\n",
                        quoteT.Date, quoteT.Low, quoteT.High, "NO BUY");
                    builder.Append(line);
                }
            }
            File.Delete(@"../../../Trend_Filter1.csv");
            File.AppendAllLines(@"../../../Trend_Filter1.csv", builder.ToString().Split("\r\n").Reverse());
            #endregion
        }
        private void CalculateIndicators()
        {
            try
            {
                Logger.Log("Started calculating indicators");
               
                ParabolicSarResults = Quotes.GetParabolicSar(0.015m).Select(x => x = new ParabolicSarResult
                {
                    IsReversal = x.IsReversal == null ? false : x.IsReversal,
                    Date = x.Date,
                    Sar = x.Sar == null ? 0 : Math.Round(x.Sar.Value, Precision)
                }).ToList();
               
                Logger.Log("Successfully calculated indicators");
            }
            catch (Exception _)
            {
                Logger.LogError(_);
            }
        }
    }
}