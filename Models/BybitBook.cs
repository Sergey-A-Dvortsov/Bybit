using Synapse.Crypto.Trading;
using Synapse.General;
using System;
using System.Collections.Generic;
using System.Text;

namespace Synapse.Crypto.Bybit
{
    public class BybitFastBook : FastBook
    {

        private long updatets; //The timestamp from the matching engine when this orderbook data is produced.
                               //It can be correlated with T from public trade channel

        public BybitFastBook(InstrumentTypes type, string symbol, double ticksize) : base(type, symbol, ticksize)
        {
            //BTCUSDT-27FEB26
            if (type == InstrumentTypes.LinearFutures || type == InstrumentTypes.InverseFutures)
            {
                if (symbol.Contains('-'))
                {
                    var temp = symbol.Split("-")[0];
                    QuoteSymbol = temp.GetQuoteSymbol();
                    BaseSymbol = temp.Replace(QuoteSymbol, "").Replace("/", "");
                }
            }
            else
            {
                QuoteSymbol = symbol.GetQuoteSymbol();
                BaseSymbol = symbol.Replace(QuoteSymbol, "").Replace("/", "");
            }

            
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
            Valid = false;

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

            logger.Debug($"UpdateWithSnapshot.{ss.data.s},Asks.Length={Asks.Length},BestAskIndex={BestAskIndex},Bids.Length={Bids.Length},BestBidIndex={BestBidIndex}");

            Valid = true;

            return true;

        }

        /// <summary>
        /// Обновляет массивы Asks и Bids при помощи измененных котировок .
        /// </summary>
        /// <param name="ss">Orderbook delta</param>
        public bool UpdateWithDelta(OrderbookResponse delta)
        {
            string smb = delta.data.s;

            if (!SnapshotReceived)
            {
                logger.Debug($"{smb}/Снапшот не был получен");
                return false;
            }

            var asks = delta.data.a;
            var bids = delta.data.b;

            string tag = "";

            try
            {



                //TODO сделать проверку на валидность данных в asks/bids. Если данные не валидны, то генерируем ошибку, выставляем Valid = false
                Valid = true;

                for (int i = 0; i < asks.Length; i++)
                {


                    var idx = GetIndex(asks[i][0], BookSides.Ask);

                    tag = $"{smb},i={i},asks.Length={asks.Length},idx={idx},Asks.Length={Asks.Length}";

                    Asks[idx] = new Quote(asks[i][0], asks[i][1]);

                    if (asks[i][0] < BestAsk.Price) // если изменилась цена лучшего Ask в сторону умешьшения
                    {
                        if (asks[i][0] == 0)
                        {
                            var index = Asks.FindIndex<Quote>(0, q => q.Size > 0);
                            if (index == null) throw new NullReferenceException(nameof(index));
                            logger.Debug($"Неоднозначная ситуация. Пришло обновление лучшего Ask в сторону умешьшения с Size = 0. Новый индекс {index.Value}");
                            BestAskIndex = index.Value;
                        }
                        else
                        {
                            BestAskIndex = idx;
                        }
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


                }

                for (int i = 0; i < bids.Length; i++)
                {

                    var idx = GetIndex(bids[i][0], BookSides.Bid);
                    Bids[idx] = new Quote(bids[i][0], bids[i][1]);

                    if (bids[i][0] > BestBid.Price) // если изменилась цена лучшего Bid в сторону увеличения
                    {
                        if (bids[i][0] == 0)
                        {
                            var index = Bids.FindIndex<Quote>(0, q => q.Size > 0);
                            if (index == null) throw new NullReferenceException(nameof(index));
                            logger.Debug($"Неоднозначная ситуация. Пришло обновление лучшего Bid в сторону увеличения с Size = 0. Новый индекс {index.Value}");
                            BestBidIndex = index.Value;
                        }
                        else
                        {
                            BestBidIndex = idx;
                        }
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

                }

                return true;
            }
            catch (Exception ex)
            {
                logger.ToError(ex, tag);
            }

            return false;
        }


    }


    public class BybitBook : OrderBook
    {
        private long updatets; //The timestamp from the matching engine when this orderbook data is produced.
                               //It can be correlated with T from public trade channel

        private long updateNum;

        public BybitBook(InstrumentTypes type, string symbol, double ticksize, int decimals) : base(type, symbol, ticksize, decimals)
        {
            //BTCUSDT-27FEB26
            if (type == InstrumentTypes.LinearFutures || type == InstrumentTypes.InverseFutures)
            {
                if (symbol.Contains('-'))
                {
                    var temp = symbol.Split("-")[0];
                    QuoteSymbol = temp.GetQuoteSymbol();
                    BaseSymbol = temp.Replace(QuoteSymbol, "").Replace("/", "");
                }
            }
            else
            {
                QuoteSymbol = symbol.GetQuoteSymbol();
                BaseSymbol = symbol.Replace(QuoteSymbol, "").Replace("/", "");
            }
          
        }

        public DateTime UpdateTime { get => updatets.UnixTimeMillisecondsToDateTime(); }

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
        private bool UpdateWithSnapshot(OrderbookResponse ss)
        {
            var asks = ss.data.a;
            var bids = ss.data.b;
            updateNum = ss.data.u;

            //TODO сделать проверку на валидность данных в asks/bids. Если данные не валидны, то генерируем ошибку, выставляем Valid = false
            Valid = false;

            Asks.Clear();

            // Заполняем массивы Asks и Bids полученными котировками из снапшота
            for (int i = 0; i < asks.Length; i++)
            {
                Asks.Add(Math.Round(asks[i][0],Decimals), asks[i][1]);
            }

            for (int i = 0; i < bids.Length; i++)
            {
                Bids.Add(Math.Round(bids[i][0], Decimals), bids[i][1]);
            }

            logger.Debug($"UpdateWithSnapshot.{ss.data.s},Asks.Count={Asks.Count}, Bids.Count={Bids.Count}");

            SnapshotReceived = true;

            SnapshotTime = DateTime.UtcNow;

            Valid = true;

            return true;

        }


        /// <summary>
        /// Обновляет массивы Asks и Bids при помощи измененных котировок .
        /// </summary>
        /// <param name="ss">Orderbook delta</param>
        private bool UpdateWithDelta(OrderbookResponse delta)
        {
            string smb = delta.data.s;

            if (!Valid)
            {
                logger.Debug($"{smb}/Снапшот не был получен или была ошибка при его создании.");
                return false;
            }


            var asks = delta.data.a;
            var bids = delta.data.b;

            string tag = "";

            try
            {

                if (delta.data.u < updateNum)
                    throw new ArgumentException("Номер обновления меньше предыдущего.");

                updateNum = delta.data.u;

                for (int i = 0; i < asks.Length; i++)
                {

                    //tag = $"{smb},i={i},asks.Length={asks.Length},idx={idx},Asks.Length={Asks.Length}";

                    if(Asks.ContainsKey(asks[i][0]))
                    {
                        if(asks[i][1] != 0)
                            Asks[asks[i][0]] = asks[i][1];
                        else
                            Asks.Remove(asks[i][0]);
                    }
                    else
                    {
                        if (asks[i][1] != 0)
                            Asks.Add(Math.Round(asks[i][0], Decimals), asks[i][1]);
                    }

                }

                for (int i = 0; i < bids.Length; i++)
                {
                    if (Bids.ContainsKey(bids[i][0]))
                    {
                        if (bids[i][1] != 0)
                            Bids[bids[i][0]] = bids[i][1];
                        else
                            Bids.Remove(bids[i][0]);
                    }
                    else
                    {
                        if (bids[i][1] != 0)
                            Bids.Add(Math.Round(bids[i][0], Decimals), bids[i][1]);
                    }

                }

                return true;
            }
            catch (Exception ex)
            {
                logger.ToError(ex, tag);
            }

            return false;
        }




    }
}
