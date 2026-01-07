using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synapse.Common;

namespace Synapse.Crypto.Bybit
{
    public class BybitResponse
    {
        [JsonProperty("retCode")]
        public int RetCode { get; set; }
        [JsonProperty("retMsg")]
        public string RetMsg { get; set; } = string.Empty;

        [JsonProperty("result")]
        public required ResultResponse Result { get; set; }

        [JsonProperty("retExtInfo")]
        public object RetExtInfo { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }
    }

    public class ResultResponse
    {
        [JsonProperty("category")]
        public required string Category { get; set; }

        [JsonProperty("nextPageCursor")]
        public string NextPageCursor { get; set; }

        [JsonProperty("list")]
        public required object List { get; set; }

    }

    //"{\"success\":true," +
    //    "\"ret_msg\":\"\"," +
    //    "\"conn_id\":\"d4anidhjocoercpjvr6g-28mw2\"," +
    //    "\"req_id\":\"bb1140bb-1654-4206-b722-aa9d6f01778d\"," +
    //    "\"op\":\"subscribe\"}" +
    //    "\0\0\0\0\0\0\0\0\0..."

    public class SoketSubscribeResponse
    {
        public bool success { set; get; } 
        public string ret_msg { set; get; }
        public string conn_id { set; get; }
        public string req_id { set; get; }
        public string op { set; get; }
    }

    public abstract class SoketDataResponse
    {
        public string topic { get; set; }
        public long ts { get; set; }
        public string type { get; set; }
    }

    public class KlineData
    {
        public long start { get; set; }
        public DateTime starttime { get => start.ToDateTimeFromMs(); }
        public long  end { get; set; }
        public DateTime endtime { get => end.ToDateTimeFromMs(); }
        public int interval { get; set; }
        public double open { get; set; }
        public double close { get; set; }
        public double high { get; set; }
        public double low { get; set; }
        public double volume { get; set; }
        public double turnover { get; set; }
        public bool confirm { get; set; }
        public long timestamp { get; set; }
        public DateTime tstime { get => timestamp.ToDateTimeFromMs(); }
    }

    public class KlineResponse : SoketDataResponse
    {
        public KlineData[] data { get; set; }
    }


    //    "topic": "kline.5.BTCUSDT",
    //    "data": [
    //    {
    //        "start": 1672324800000,
    //        "end": 1672325099999,
    //        "interval": "5",
    //        "open": "16649.5",
    //        "close": "16677",
    //        "high": "16677",
    //        "low": "16608",
    //        "volume": "2.081",
    //        "turnover": "34666.4005",
    //        "confirm": false,
    //        "timestamp": 1672324988882
    //    }
    //],
    //"ts": 1672324988882,
    //"type": "snapshot"

}
