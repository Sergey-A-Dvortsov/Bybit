using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synapse.Crypto.Bybit
{
    public struct Candle
    {

        /// <summary>
        /// Open candle time.
        /// </summary>
       public DateTime OpenTime { get; set; }

        /// <summary>
        /// Open price.
        /// </summary>
        public double Open { get; set; }

        /// <summary>
        /// Hight price.
        /// </summary>
        public double High { get; set; }

        /// <summary>
        /// Low price.
        /// </summary>
        public double Low { get; set; }

        /// <summary>
        /// Close price.
        /// </summary>
        public double Close { get; set; }

        /// <summary>
        /// Base coin volume.
        /// </summary>
        public double Volume { get; set; }

        /// <summary>
        /// Quote coin volume.
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// true if candle is finished
        /// </summary>
        public bool Confirm { get; set; }

        /// <summary>
        /// true if candle is not historical
        /// </summary>
        public bool IsRealtime { get; set; } 

        public override string ToString()
        {
            return $"{OpenTime};{Open};{High};{Low};{Close};{Volume};{Value}";
        }

        public static Candle GetCandle(string line)
        {
            return new Candle
            {
                OpenTime = DateTime.Parse(line.Split(';')[0]),
                Open = double.Parse(line.Split(';')[1]),
                High = double.Parse(line.Split(';')[2]),
                Low = double.Parse(line.Split(';')[3]),
                Close = double.Parse(line.Split(';')[4]),
                Volume = double.Parse(line.Split(';')[5]),
                Value = double.Parse(line.Split(';')[6]),
                Confirm = true,
                IsRealtime = false
            };

        }

        public Candle Clone(bool confirm, bool isRealtime = false)
        {
            return new Candle
            {
                OpenTime = this.OpenTime,
                Open = this.Open,
                High = this.High,
                Low = this.Low,
                Close = this.Close,
                Volume = this.Volume,
                Value = this.Value,
                Confirm = confirm,
                IsRealtime = isRealtime
            };

        }

    }
}
