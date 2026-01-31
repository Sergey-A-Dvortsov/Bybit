using Synapse.Crypto.Trading;
using Synapse.General;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Synapse.Crypto.Bybit
{

    public readonly record struct Quote(double Price, double Size);

    public class FastBook
    {
        private readonly double ticksize;

        private double zeroAskPrice;
        private double zeroBidPrice;

        public FastBook(string symbol, double ticksize) 
        {
            Symbol = symbol;
            this.ticksize = ticksize;
        }

        public string Symbol { get; private set;  }

        public bool Valid { get; private set; }

        public Quote[] Asks { get; private set; }

        public Quote[] Bids { get; private set; }

        public int BestAskIndex { get; private set; }

        public int BestBidIndex { get; private set; }

        /// <summary>
        /// Полностью обновляет массивы Asks и Bids.
        /// </summary>
        /// <param name="ss">Orderbook snapshot</param>
        public void UpdateWithSnapshot(OrderbookResponse ss)
        {
            var asks = ss.data.a;
            var bids = ss.data.b;

            //TODO сделать проверку на валидность данных в asks/bids. Если данные не валидны, то генерируем ошибку, выставляем Valid = false
            Valid = true;

            // Создаем массивы с шагом цены равным ticksize и дипазоном цен на 1% больше/меньше текущих первой и последней цены снапшота 
            var firstAsk = asks.First();
            var lastAsk = asks.Last();
            zeroAskPrice = firstAsk[0] - ((firstAsk[0] / 100).PriceRound(ticksize)); // цена первого элемента будущего массива 
            double extLastAsk = lastAsk[0] + ((lastAsk[0] / 100).PriceRound(ticksize));
            int askdepth = (int)((extLastAsk - zeroAskPrice) /ticksize);

            var firstBid = bids.First();
            var lastBid = bids.Last();
            zeroBidPrice = firstBid[0] + ((firstBid[0] / 100).PriceRound(ticksize));
            double extLastBid = lastBid[0] - ((lastBid[0] / 100).PriceRound(ticksize));
            int biddepth = (int)((zeroBidPrice - extLastBid) / ticksize);


            int depth = Math.Max(askdepth, biddepth);

            Asks = new Quote[depth];
            Bids = new Quote[depth];

            for (int i = 0; i < depth; i++)
            {
                double askprice = zeroAskPrice + ticksize * i;
                double bidprice = zeroBidPrice - ticksize * i;
                double asksize = 0, bidsize = 0;

                if (askprice == firstAsk[0])
                    BestAskIndex = i;

                if (bidprice == firstBid[0])
                    BestBidIndex = i;

                Asks[i] = new Quote(askprice, asksize);
                Bids[i] = new Quote(bidprice, bidsize);
            }

            for (int i = 0; i < asks.Length; i++)
            {
                var idx = GetIndex(asks[i][0], BookSides.Ask);
                Asks[idx] = new Quote(asks[i][0], asks[i][1]);
            }

            for (int i = 0; i < bids.Length; i++)
            {
                var idx = GetIndex(bids[i][0], BookSides.Bid);
                Bids[idx] = new Quote(bids[i][0], bids[i][1]);
            }

        }

        /// <summary>
        ///  Возвращает индекс котировки
        /// </summary>
        /// <param name="price">цена</param>
        /// <param name="side">Bid/Ask</param>
        /// <returns></returns>
        private int GetIndex(double price, BookSides side)
        {
            int index = side == BookSides.Ask ? 
                (int)Math.Round(((price - zeroAskPrice)/ticksize),0) : 
                (int)Math.Round(((zeroBidPrice - price)/ticksize),0);
            return index;
        }

    }
}


//public readonly record struct Point(double X, double Y);
//// или просто
//public readonly struct Point
//{
//    public double X;
//    public double Y;

//    // если хотите методы — добавляйте, но без виртуальных
//}

//Point[] points = new Point[1_000_000];

//// горячий цикл
//for (int i = 0; i < points.Length; i++)
//{
//    ref var p = ref points[i];          // ← очень важно: ref-доступ
//    p.X += velocityX;
//    p.Y += velocityY;
//}