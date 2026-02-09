using bybit.net.api.Models;
using Synapse.Crypto.Trading;
using Synapse.General;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synapse.Crypto.Bybit
{
    // Copyright(c) [2026], [Sergey Dvortsov]
    public static class Helpers
    {
        /// <summary>
        /// Converts from native bybit class to accessible candle structure
        /// </summary>
        /// <param name="kline"></param>
        /// <returns></returns>
        public static Candle ToCandle(this KlineData kline, bool isRealtime = false)
        {
            return new Candle
            {
                OpenTime = kline.start.UnixTimeMillisecondsToDateTime(),
                Open = kline.open,
                High = kline.high,
                Low = kline.low,
                Close = kline.close,
                Volume = kline.volume,
                Value = kline.turnover,
                IsRealtime = isRealtime,
                Confirm = kline.confirm
            };
        }


        //public static InstrumentTypes? GetInstrumentType(this Category category)
        //{

        //    if(category == Category.INVERSE)
        //        return InstrumentTypes.Inverse;
        //    else if(category == Category.LINEAR)
        //        return InstrumentTypes.Linear;
        //    else if (category == Category.SPOT)
        //        return InstrumentTypes.Spot;
        //    return null;
        //}

        public static string GetSpotSymbol(string symbol)
        {
            var quoteSmb = symbol.GetQuoteSymbol();
            var baseSmb = symbol.Replace(quoteSmb, "");
            return $"{baseSmb}/{quoteSmb}";
        }


    }
}
