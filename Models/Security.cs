using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synapse.General;

namespace Synapse.Crypto.Bybit
{

    public struct ContractType
    {
        private ContractType(string value)
        {
            this.Value = value;
        }

        public static ContractType InversePerpetual { get => new("InversePerpetual"); }
        public static ContractType LinearPerpetual { get => new("LinearPerpetual"); }
        public static ContractType LinearFutures { get => new("LinearFutures"); }
        public static ContractType InverseFutures { get => new("InverseFutures"); }
        public static ContractType Spot { get => new("Spot"); }
        public string Value { get; private set; }
        public static implicit operator string(ContractType enm) => enm.Value;
        public override readonly string ToString() => this.Value.ToString();
    }

    public class LeverageFilter
    {
        //>> minLeverage string Minimum leverage
        [JsonProperty("minLeverage")]
        public double MinLeverage { get; set; }

        //>> maxLeverage string Maximum leverage
        [JsonProperty("maxLeverage")]
        public double MaxLeverage { get; set; }

        //>> leverageStep string The step to increase/reduce leverage
        [JsonProperty("leverageStep")]
        public double TotalLeverage { get; set; }
    }

    public class PriceFilter
    {
        //>> minPrice string Minimum order price
        [JsonProperty("minPrice")]
        public double MinPrice { get; set; }

        //>> maxPrice string Maximum order price
        [JsonProperty("maxPrice")]
        public double MaxPrice { get; set; }

        //>> tickSize string The step to increase/reduce order price
        [JsonProperty("tickSize")]
        public double TickSize { get; set; }
    }

    public class LotSizeFilter
    {
        [JsonProperty("basePrecision")]
        public double BasePrecision { get; set; }

        [JsonProperty("quotePrecision")]
        public double QuotePrecision { get; set; }

        [JsonProperty("minOrderQty")]
        public double MinOrderQty { get; set; }

        [JsonProperty("maxOrderQty")]
        public double MaxOrderQty { get; set; }

        [JsonProperty("minOrderAmt")]
        public double MinOrderAmt { get; set; }

        [JsonProperty("maxOrderAmt")]
        public double MaxOrderAmt { get; set; }
    }

    public class RiskParameters
    {
        //>> priceLimitRatioX string Price limit ratio X
        [JsonProperty("priceLimitRatioX")]
        public double PriceLimitRatioX { get; set; }
        //>> priceLimitRatioY string Price limit ratio Y
        [JsonProperty("priceLimitRatioY")]
        public double PriceLimitRatioY { get; set; }
    }

    public class BybitSecurity
    {
        [JsonProperty("symbol")]
        public string Symbol { get; set; } = string.Empty;

        //> contractType string Contract type
        [JsonProperty("contractType")]
        public string ContractType { get; set; }

        [JsonProperty("baseCoin")]
        public string BaseCoin { get; set; } = string.Empty;

        [JsonProperty("quoteCoin")]
        public string QuoteCoin { get; set; } = string.Empty;

        [JsonProperty("marginTrading")]
        public string MarginTrading { get; set; }

        [JsonProperty("unifiedMarginTrade")]
        public bool UnifiedMarginTrade { get; set; }

        [JsonProperty("stTag")]
        public string StTag { get; set; } = string.Empty;

        [JsonProperty("launchTime")]
        public long LaunchTime { get; set; }

        public DateTime StartTime { get => LaunchTime.UnixTimeMillisecondsToDateTime(); }

        [JsonProperty("deliveryTime")]
        public long DeliveryTime { get; set; }

        //> deliveryFeeRate string Delivery fee rate
        [JsonProperty("deliveryFeeRate")]
        public double? DeliveryFeeRate { get; set; }

        //> priceScale string Price scale
        [JsonProperty("priceScale")]
        public int PriceScale { get; set; }

        public int PriceDecimals 
        { 
            get => PriceFilter.TickSize.GetDecimals();
        }

        [JsonProperty("leverageFilter")]
        public LeverageFilter LeverageFilter { get; set; }

        [JsonProperty("priceFilter")]
        public PriceFilter PriceFilter { get; set; }

        [JsonProperty("lotSizeFilter")]
        public LotSizeFilter LotSizeFilter { get; set; }

        public bool HaveSpot { get; set; } = false;

    }


}
