using NLog;
using Synapse.Crypto.Trading;
using Synapse.General;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Synapse.Crypto.Bybit
{

    public readonly record struct Quote(double Price, double Size);

    public abstract class FastBook
    {
        private readonly double ticksize;
        private readonly int decimals;
        private readonly double offset; // смещение от границ котировок книги заявок в %;
       
        public FastBook(string symbol, double ticksize, double offset = 0.01)
        {
            Symbol = symbol;
            this.ticksize = ticksize;
            this.decimals = ticksize.GetDecimals();
            this.offset = offset; 
        }

        public Logger logger = LogManager.GetCurrentClassLogger();

        public string Symbol { get; private set; }

        public bool Valid { get; set; }

        public Quote[] Asks { get; private set; }

        public Quote[] Bids { get; private set; }

        public Quote BestAsk { get => Asks[BestAskIndex]; }

        public Quote BestBid { get => Bids[BestBidIndex]; }

        internal int BestAskIndex { get; set; }

        internal int BestBidIndex { get; set; }

        public abstract DateTime UpdateTime { get; }

        public TimeSpan Delay { get; set; }

        internal double ZeroAskPrice;
        internal double ZeroBidPrice;

        internal int Depth;

        public bool UpdateWithSnapshot(Dictionary<BookSides, double[]> prices)
        {

            var prcs = prices[BookSides.Ask];

            ZeroAskPrice = GetOffsetPrice(prcs[0], -1); // цена первой котировки будущего массива Asks
            double lastAsk = GetOffsetPrice(prcs[1], 1); // цена последней котировки будущего массива Asks
            int askdepth = (int)((lastAsk - ZeroAskPrice) / ticksize);

            prcs = prices[BookSides.Bid];
            ZeroBidPrice = GetOffsetPrice(prcs[0], 1); // цена первой котировки будущего массива Bids
            double lastBid = GetOffsetPrice(prcs[1], -1); // цена последней котировки будущего массива Bids

            int biddepth = (int)((ZeroBidPrice - lastBid) / ticksize);

            Depth = Math.Max(askdepth, biddepth); // размер будущих массивов

            Asks = new Quote[Depth];
            Bids = new Quote[Depth];

            for (int i = 0; i < Depth; i++)
            {
                double askprice = Math.Round(ZeroAskPrice + ticksize * i, decimals);
                double bidprice = Math.Round(ZeroBidPrice - ticksize * i, decimals);

                if (askprice == prices[BookSides.Ask][0])
                    BestAskIndex = i;

                if (bidprice == prices[BookSides.Bid][0])
                    BestBidIndex = i;

                Asks[i] = new Quote(askprice, 0);
                Bids[i] = new Quote(bidprice, 0);
            }

            return true;

        }

        /// <summary>
        ///  Возвращает цену со смещением offset от заданной цены вверх, если k=1, или вниз, если k=-1.
        /// </summary>
        /// <param name="price"></param>
        /// <param name="k">задает направление смещения, вверх (k=1), вниз, если k=-1</param>
        /// <returns></returns>
        private double GetOffsetPrice(double price, int k)
        {
            return Math.Round((price + k * (price * offset)).PriceRound(ticksize), decimals);
        }

        ///// <summary>
        ///// Обновляет массивы Asks и Bids при помощи измененных котировок .
        ///// </summary>
        ///// <param name="ss">Orderbook delta</param>
        //public bool UpdateWithDelta(OrderbookResponse delta)
        //{
        //    var asks = delta.data.a;
        //    var bids = delta.data.b;

        //    try
        //    {

        //        //TODO сделать проверку на валидность данных в asks/bids. Если данные не валидны, то генерируем ошибку, выставляем Valid = false
        //        Valid = true;

        //        for (int i = 0; i < asks.Length; i++)
        //        {
        //            var idx = GetIndex(asks[i][0], BookSides.Ask);

        //            if (asks[i][0] < BestAsk.Price) // если изменилась цена лучшего Ask в сторону умешьшения
        //            {
        //                if (asks[i][0] == 0) throw new Exception("Неоднозначная ситуация. Пришло обновление лучшего Ask в сторону умешьшения с Size = 0. Возможно нарушена целостность книги заявок.");
        //                BestAskIndex = idx;
        //            }
        //            else if (asks[i][0] == BestAsk.Price) // если изменился размер лучшего Ask или цена в сторону увеличения 
        //            {
        //                if (asks[i][0] == 0) // изменилась цена лучшего Ask в сторону увеличения, ищем новый лучший аск 
        //                {
        //                    var index = Asks.FindIndex<Quote>(idx + 1, q => q.Size > 0);

        //                    if (index == null)
        //                        throw new NullReferenceException(nameof(index));

        //                    BestAskIndex = index.Value;
        //                }
        //            }

        //            Asks[idx] = new Quote(asks[i][0], asks[i][1]);

        //        }

        //        for (int i = 0; i < bids.Length; i++)
        //        {

        //            var idx = GetIndex(bids[i][0], BookSides.Bid);

        //            if (bids[i][0] > BestBid.Price) // если изменилась цена лучшего Bid в сторону увеличения
        //            {
        //                if (bids[i][0] == 0) throw new Exception("Неоднозначная ситуация. Пришло обновление лучшего Bid в сторону увеличения с Size = 0. Возможно нарушена целостность книги заявок.");
        //                BestBidIndex = idx;
        //            }
        //            else if (bids[i][0] == BestBid.Price) // если изменился размер лучшего Bid или цена в сторону уменьшения
        //            {
        //                if (bids[i][0] == 0) // изменилась цена лучшего Bid в сторону уменьшения, ищем новый лучший Bid
        //                {
        //                    var index = Bids.FindIndex<Quote>(idx + 1, q => q.Size > 0);

        //                    if (index == null)
        //                        throw new NullReferenceException(nameof(index));

        //                    BestBidIndex = index.Value;
        //                }
        //            }

        //            Bids[idx] = new Quote(bids[i][0], bids[i][1]);

        //        }

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        logger.ToError(ex);
        //    }

        //    return false;
        //}

       


        /// <summary>
        ///  Возвращает индекс котировки
        /// </summary>
        /// <param name="price">цена</param>
        /// <param name="side">Bid/Ask</param>
        /// <returns></returns>
        public int GetIndex(double price, BookSides side)
        {
            int index = side == BookSides.Ask ?
                (int)Math.Round(((price - ZeroAskPrice) / ticksize), 0) :
                (int)Math.Round(((ZeroBidPrice - price) / ticksize), 0);
            return index;
        }

    }

    public class BybitFastBook : FastBook
    {

        private long updatets; //The timestamp from the matching engine when this orderbook data is produced.
                               //It can be correlated with T from public trade channel

        public BybitFastBook(string symbol, double ticksize) : base(symbol, ticksize)
        {
        }

        public override DateTime UpdateTime { get => updatets.UnixTimeMillisecondsToDateTime(); }

        public bool Update(OrderbookResponse resp)
        {
            updatets = resp.cts;
            Delay = DateTime.UtcNow - UpdateTime; 

            if (resp.type == "snapshot")
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

            Dictionary<BookSides, double[]> prices = new()
            {
                {BookSides.Ask, [asks.First()[0], asks.Last()[0]]},
                { BookSides.Bid, [bids.First()[0], bids.Last()[0]]}
            };

            // Создаем массивы с шагом цены равным ticksize и дипазоном цен на величину offset больше/меньше текущих первой и последней цены снапшота 
            var result = UpdateWithSnapshot(prices);

            if (result == false) return false;

            // Заполняем массивы Asks и Bids полученными котировками из снапшота
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

        ///// <summary>
        /////  Возвращает индекс котировки
        ///// </summary>
        ///// <param name="price">цена</param>
        ///// <param name="side">Bid/Ask</param>
        ///// <returns></returns>
        //private int GetIndex(double price, BookSides side)
        //{
        //    int index = side == BookSides.Ask ?
        //        (int)Math.Round(((price - zeroAskPrice) / ticksize), 0) :
        //        (int)Math.Round(((zeroBidPrice - price) / ticksize), 0);
        //    return index;
        //}

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

//The timestamp from the matching engine when this orderbook data is produced. It can be correlated with T from public trade channel