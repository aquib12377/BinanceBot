using System;
using System.Threading;
using System.Threading.Tasks;
using WTelegram;
using System.Linq;

namespace Binance_Trader
{
    /// <summary>
    /// This branch is used to trade based on 5 Indicators for confirming the trend in short term market 
    /// and place the trade.
    /// Indicators - MACD, RSI X2, EMA X2.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            var binance = new Binance();
            ConsoleSpiner spin = new ConsoleSpiner();
            await binance.Initialize("BNBUSDT",2);

            while (true)
            {
                await binance.CollectDataset();
                spin.Turn();
            }
        }
    }
}
