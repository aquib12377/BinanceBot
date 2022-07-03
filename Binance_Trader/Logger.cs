using Binance.Net.Interfaces;
using CryptoExchange.Net.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Binance_Trader
{
    public class Logger
    {
        private DateTime DateTime
        {
            get
            {
                return DateTime.Now;
            }
        } 
        public Logger(){}
        public async void LogError(Exception e)
        {
            string error = string.Format("[{0}] Error while logging : {1} \r\n Stacktrace: {2}",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    e.Message ?? e.InnerException.ToString(), e.StackTrace);
            File.AppendAllText("ErrorLog.txt", error);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(error);
            Console.ResetColor();
            await Task.Delay(5000);
        }
        public void LogSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(string.Format("[{0}] {1}", DateTime, message));
            Console.ResetColor();
        }
        public void Log(string message)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(string.Format("[{0}] {1}", DateTime, message));
                Console.ResetColor();
            }
            catch (Exception _)
            {
                LogError(_);
            }
        }
        public void LogCandle(IEnumerable<IBinanceKline> klines)
        {
            if (klines.Count() != 0)
                foreach (var kline in klines)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Date Open High Low Close Volume");
                    Console.WriteLine(string.Format("{0} {1} {2} {3} {4} {5}", kline.OpenTime, kline.OpenPrice, kline.HighPrice, kline.LowPrice, kline.ClosePrice, kline.QuoteVolume));
                    Console.ResetColor();
                }
            else
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"No Kline " + DateTime.Now.ToString());
                Console.ResetColor();
            }
        }
        public void LogSignal(bool _long,bool noBuy, decimal price,string message)
        {
            if(noBuy)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine(string.Format("[{0}] Price {2} - {1}", DateTime, message,price));
                Console.ResetColor();
            }
            else if (_long)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(string.Format("[{0}] Price {2} - {1}", DateTime, message, price));
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(string.Format("[{0}] Price {2} - {1}", DateTime, message, price));
                Console.ResetColor();
            }

        }
    }
}
