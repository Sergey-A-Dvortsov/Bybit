using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synapse.Crypto.Bybit
{
    public class FundingRate
    {
        //        > symbol string Symbol name
        [JsonProperty("symbol")]
        public required string Symbol { get; set; }

        //> fundingRate string Funding rate
        [JsonProperty("fundingRate")]
        public required double Rate { get; set; }

        //> fundingRateTimestamp string Funding rate timestamp(ms)
        [JsonProperty("fundingRateTimestamp")]
        public required long Timestamp { get; set; }

        public DateTime Time
        {
            get
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).DateTime;
            }
        }

        public override string ToString()
        {
            return $"{Time};{Rate}";
        }

    }
}
