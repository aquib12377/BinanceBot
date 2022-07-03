using System;
using System.Threading;
using System.Threading.Tasks;
using WTelegram;
using System.Linq;

namespace Binance_Trader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var binance = new Binance();
            ConsoleSpiner spin = new ConsoleSpiner();
            await binance.Initialize("BNBUSDT",2);

            while (true)
            {
                await binance.Execute();
                spin.Turn();
            }
        }
    }
}
