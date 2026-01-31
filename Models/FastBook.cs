using NLog;
using Synapse.Crypto.Trading;
using Synapse.General;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
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

        private Logger logger = LogManager.GetCurrentClassLogger();

        public FastBook(string symbol, double ticksize)
        {
            Symbol = symbol;
            this.ticksize = ticksize;
        }

        public string Symbol { get; private set; }

        public bool Valid { get; private set; }

        public Quote[] Asks { get; private set; }

        public Quote[] Bids { get; private set; }

        public Quote BestAsk { get => Asks[BestAskIndex]; }

        public Quote BestBid { get => Bids[BestBidIndex]; }

        public int BestAskIndex { get; private set; }

        public int BestBidIndex { get; private set; }

        public bool Update(OrderbookResponse resp)
        {
            if(resp.type == "snapshot")
                 return UpdateWithSnapshot(resp);
            else if (resp.type == "delta")
                 return UpdateWithDelta(resp);

            return false;
        }

        /// <summary>
        /// Полностью обновляет массивы Asks и Bids при помощи снапшота книги заявок.
        /// </summary>
        /// <param name="ss">Orderbook snapshot</param>
        public bool UpdateWithSnapshot(OrderbookResponse ss)
        {
            var asks = ss.data.a;
            var bids = ss.data.b;

            //TODO сделать проверку на валидность данных в asks/bids. Если данные не валидны, то генерируем ошибку, выставляем Valid = false
            Valid = true;

            // Создаем массивы с шагом цены равным ticksize и дипазоном цен на 1% больше/меньше текущих первой и последней цены снапшота 

            var firstAsk = asks.First();
            var lastAsk = asks.Last();
            zeroAskPrice = firstAsk[0] - ((firstAsk[0] / 100).PriceRound(ticksize)); // цена первой котировки будущего массива Asks
            double extLastAsk = lastAsk[0] + ((lastAsk[0] / 100).PriceRound(ticksize));
            int askdepth = (int)((extLastAsk - zeroAskPrice) / ticksize);

            var firstBid = bids.First();
            var lastBid = bids.Last();
            zeroBidPrice = firstBid[0] + ((firstBid[0] / 100).PriceRound(ticksize)); // цена первой котировки будущего массива Bids
            double extLastBid = lastBid[0] - ((lastBid[0] / 100).PriceRound(ticksize));
            int biddepth = (int)((zeroBidPrice - extLastBid) / ticksize);

            int depth = Math.Max(askdepth, biddepth); // расчетный размер будущих массивов

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

            return true;

        }

        /// <summary>
        /// Обновляет массивы Asks и Bids при помощи измененных котировок .
        /// </summary>
        /// <param name="ss">Orderbook delta</param>
        public bool UpdateWithDelta(OrderbookResponse delta)
        {
            var asks = delta.data.a;
            var bids = delta.data.b;

            try
            {

                //TODO сделать проверку на валидность данных в asks/bids. Если данные не валидны, то генерируем ошибку, выставляем Valid = false
                Valid = true;

                for (int i = 0; i < asks.Length; i++)
                {
                    var idx = GetIndex(asks[i][0], BookSides.Ask);

                    if (asks[i][0] < BestAsk.Price) // если изменилась цена лучшего Ask в сторону умешьшения
                    {
                        if (asks[i][0] == 0) throw new Exception("Неоднозначная ситуация. Пришло обновление лучшего Ask в сторону умешьшения с Size = 0. Возможно нарушена целостность книги заявок.");
                        BestAskIndex = idx;
                    }
                    else if (asks[i][0] == BestAsk.Price) // если изменился размер лучшего Ask или цена в сторону увеличения 
                    {
                        if (asks[i][0] == 0) // изменилась цена лучшего Ask в сторону увеличения, ищем новый лучший аск 
                        {
                            var index = Asks.FindIndex<Quote>(idx + 1, q => q.Size > 0);

                            if (index == null)
                                throw new NullReferenceException(nameof(index));

                            BestAskIndex = index.Value;
                        }
                    }

                    Asks[idx] = new Quote(asks[i][0], asks[i][1]);

                }

                for (int i = 0; i < bids.Length; i++)
                {

                      var idx = GetIndex(bids[i][0], BookSides.Bid);

                    if (bids[i][0] > BestBid.Price) // если изменилась цена лучшего Bid в сторону увеличения
                    {
                        if (bids[i][0] == 0) throw new Exception("Неоднозначная ситуация. Пришло обновление лучшего Bid в сторону увеличения с Size = 0. Возможно нарушена целостность книги заявок.");
                          BestBidIndex = idx;
                    }
                    else if (bids[i][0] == BestBid.Price) // если изменился размер лучшего Bid или цена в сторону уменьшения
                    {
                        if (bids[i][0] == 0) // изменилась цена лучшего Bid в сторону уменьшения, ищем новый лучший Bid
                        {
                            var index = Bids.FindIndex<Quote>(idx + 1, q => q.Size > 0);

                            if (index == null)
                                throw new NullReferenceException(nameof(index));

                            BestBidIndex = index.Value;
                        }
                    }

                    Bids[idx] = new Quote(bids[i][0], bids[i][1]);

                }

                return true;
            }
            catch (Exception ex)
            {
                logger.ToError(ex);
            }

            return false;
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
                (int)Math.Round(((price - zeroAskPrice) / ticksize), 0) :
                (int)Math.Round(((zeroBidPrice - price) / ticksize), 0);
            return index;
        }

        private int? FindIndex<T>(T[] array, int startIndex, Func<T, bool> predicate)
        {
            for (int i = startIndex; i < array.Length; i++)
            {
                if (predicate(array[i]))
                    return i;
            }
            return null;
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