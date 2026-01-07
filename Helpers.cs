using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synapse.Common;

namespace Synapse.Crypto.Bybit
{
    public static class Helpers
    {
        public static Candle ToCandle(this KlineData kline)
        {
            return new Candle
            {
                OpenTime = kline.start.ToDateTimeFromMs(),
                Open = kline.open,
                High = kline.high,
                Low = kline.low,
                Close = kline.close,
                Volume = kline.volume,
                Value = kline.turnover,
                IsRealtime = true,
                Confirm = kline.confirm
            };
        }
    }
}
