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