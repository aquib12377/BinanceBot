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
        #region Signal variables
        private static readonly string _signalFile = "PlacedOrder.txt";
        #endregion
        #region Indicators variables
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
        private string _coin = string.Empty;
        private BinanceClient Client;
        private Logger Logger;
        private decimal Leverage;
        private decimal UseBalancePercentage;
        #endregion
        public async Task Initialize(string coinName, int precision = 3, decimal leverage = 10.0m,
                                     decimal useBalancePercentage = 50.0m)
        {
            Logger = new Logger();
            try
            {
                #region Initializing Binance Clients for API interaction
                Client = new BinanceClient(new BinanceClientOptions()
                {
                    ApiCredentials = new ApiCredentials("your Binance Key"
                        , "Your Binance secret")
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
                Leverage = leverage;
                UseBalancePercentage = useBalancePercentage;
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
        public async Task PlaceOrder(bool _long, string coin)
        {
            #region fetching balance and coin price
            var usdtb = await Client.UsdFuturesApi.Account.GetBalancesAsync();
            var usdtBalance = usdtb.Data.Where(x => x.Asset == "USDT").FirstOrDefault().AvailableBalance;
            var cp = await Client.UsdFuturesApi.ExchangeData.GetMarkPriceAsync(coin);
            var coinPrice = cp.Data.MarkPrice;
            #endregion
            #region Calculating position size based on 15x Leverage
            var positionSize = usdtBalance * UseBalancePercentage * Leverage;
            #endregion
            #region Placing  order with TP 0.4% and SL 0.2% of price change profit will depend on the leverage you use.

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
                        stopLossPrice = Math.Round(coinPrice - ((0.2m / 100) * coinPrice),Precision);
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
        public async Task Execute()
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
                    await PlaceOrder(true, _coin);
                    prevLL = true;
                    prevSS = false;
                    File.AppendAllText(_signalFile, string.Format("{0},{1},{2},{3}\r\n",
                                                    quote.Date, quote.Low, quote.High, "LONG"));
                }
                else if (_short && (prevSS == null || prevSS != true) && IsFirsTime == false)
                {
                    await PlaceOrder(false, _coin);
                    prevSS = true;
                    prevLL = false;
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
            catch (Exception e)
            {
                Logger.LogError(e);
            }
        }
    }
}