using bybit.net.api;
using bybit.net.api.ApiServiceImp;
using bybit.net.api.Models;
using bybit.net.api.Models.Market;
using bybit.net.api.WebSocketStream;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NLog;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.WebSockets;
using Synapse.General;

namespace Synapse.Crypto.Bybit
{
    // Copyright(c) [2026], [Sergey Dvortsov]
    /// <summary>
    /// API client bybit
    /// </summary>
    public class BybitClient
    {
        private readonly BybitMarketDataService market = null;

        private readonly Dictionary<string, DateTime> candleTimes = [];

        private readonly TimeSpan candleBrake = TimeSpan.FromSeconds(5);

        public static BybitClient Instance { get; private set; }

        public BybitClient()
        {
            market = new(url: BybitConstants.HTTP_MAINNET_URL, debugMode: false);
            Instance = this;
        }

        #region events

        /// <summary>
        /// Event of candle update or new candle
        /// </summary>
        public event Action<string, TimeFrames, List<Candle>> CandleUpdate = delegate { };

        private void OnCandleUpdate(string symbol, TimeFrames frame, List<Candle> candles)
        {
            CandleUpdate?.Invoke(symbol, frame, candles);
        }

        #endregion

        /// <summary>
        /// Logger
        /// </summary>
        public Logger Logger { get; private set; } = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// List of instruments
        /// </summary>
        public List<BybitSecurity> Securities { get; private set; } = [];

        /// <summary>
        /// Subscriptions to streaming data
        /// </summary>
        public Dictionary<string, Tuple<BybitLinearWebSocket, CancellationToken>> Subscriptions { get; private set; } = [];

        public static void Init()
        {
            Instance = new BybitClient();
        }

        /// <summary>
        /// Loading instrument information.
        /// </summary>
        /// <param name="categories">instrument type or null for all instruments</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        public async Task<List<BybitSecurity>> LoadSecurity(IEnumerable<Category> categories = null)
        {
            try
            {
                Securities.Clear();

                var response = await market.GetInstrumentInfo(category: Category.SPOT, symbol: null, status: InstrumentStatus.Trading, null, 1000) ?? throw new NullReferenceException("Failed to take spotResponse");
                var tempResult = JsonConvert.DeserializeObject<BybitResponse>(response);

                if (tempResult?.RetMsg == "OK")
                {
                    var spots = JsonConvert.DeserializeObject<BybitSecurity[]>(tempResult.Result.List.ToString());

                    foreach (var item in spots)
                        item.ContractType = ContractType.Spot;
                    Securities.AddRange(spots);
                }
                else
                {
                    throw new Exception($"Failed to load spot securities: {tempResult?.RetMsg}");
                }

                if (categories == null || categories.Any(c => c == Category.LINEAR))
                {
                    response = await market.GetInstrumentInfo(category: Category.LINEAR, symbol: null, status: InstrumentStatus.Trading, null, 1000);
                    if (response == null)
                        throw new NullReferenceException("Failed to take linFutResponse");

                    tempResult = JsonConvert.DeserializeObject<BybitResponse>(response);

                    if (tempResult?.RetMsg == "OK")
                    {
                        var linFuts = JsonConvert.DeserializeObject<BybitSecurity[]>(tempResult.Result.List.ToString());

                        foreach (var item in linFuts)
                        {
                            item.HaveSpot = Securities.Any(s => s.ContractType == ContractType.Spot && s.BaseCoin == item.BaseCoin && s.QuoteCoin == item.QuoteCoin);
                        }

                        Securities.AddRange(linFuts);
                    }
                    else
                    {
                        Logger.Error($"Failed to load linear futures: {tempResult?.RetMsg}");
                    }
                }

                if (categories == null || categories.Any(c => c == Category.INVERSE))
                {
                    response = await market.GetInstrumentInfo(category: Category.INVERSE, symbol: null, status: InstrumentStatus.Trading, null, 1000);

                    if (response == null)
                        throw new NullReferenceException("Failed to take invFutResponse");

                    tempResult = JsonConvert.DeserializeObject<BybitResponse>(response);

                    if (tempResult?.RetMsg == "OK")
                    {
                        var invFuts = JsonConvert.DeserializeObject<BybitSecurity[]>(tempResult.Result.List.ToString());

                        foreach (var item in invFuts)
                        {
                            item.HaveSpot = Securities.Any(s => s.ContractType == ContractType.Spot && s.BaseCoin == item.BaseCoin && s.QuoteCoin == item.QuoteCoin);
                        }

                        Securities.AddRange(invFuts);
                    }
                }

                return Securities;
            }
            catch (Exception ex)
            {
                Logger.ToError(ex);
            }

            return null;

        }

        #region candles

        /// <summary>
        /// Loads candles for a given instrument, interval, start and end date.
        /// </summary>
        /// <param name="category">instrument type</param>
        /// <param name="symbol">instrument symbol</param>
        /// <param name="interval">interval</param>
        /// <param name="startTime">start time</param>
        /// <param name="endTime">end time</param>
        /// <param name="limit">limit for loaded bars</param>
        /// <returns></returns>
        public async Task<Candle[]?> GetCandlesHistory(Category category, string symbol, MarketInterval interval, DateTime? startTime = null, DateTime? endTime = null, int? limit = 1000)
        {
            try
            {

                var response = await market.GetMarketKline(
                    category: category,
                    symbol: symbol,
                    interval: interval,
                    start: startTime?.ToUnixTimeSeconds(),
                    end: endTime?.ToUnixTimeSeconds(),
                    limit: limit);

                if (string.IsNullOrEmpty(response))
                    throw new Exception($"Failed to load candles history for {symbol} in {category} category.");

                var result = JsonConvert.DeserializeObject<BybitResponse>(response);

                Candle[]? candles = null;

                if (result?.RetMsg == "OK")
                {
                    var items = JsonConvert.DeserializeObject<object[]>(result.Result.List.ToString());
                    candles = new Candle[items.Length];

                    for (var i = 0; i < items.Length; i++)
                    {
                        var item = JsonConvert.DeserializeObject<object[]>(items[i].ToString());

                        candles[i] = new Candle
                        {
                            OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(item[0])).UtcDateTime,
                            Open = Convert.ToDouble(item[1], CultureInfo.InvariantCulture),
                            High = Convert.ToDouble(item[2], CultureInfo.InvariantCulture),
                            Low = Convert.ToDouble(item[3], CultureInfo.InvariantCulture),
                            Close = Convert.ToDouble(item[4], CultureInfo.InvariantCulture),
                            Volume = Convert.ToDouble(item[5], CultureInfo.InvariantCulture),
                            Value = Convert.ToDouble(item[6], CultureInfo.InvariantCulture),
                            Confirm = true,
                            IsRealtime = false
                        };

                    }

                }

                return candles;

            }
            catch (Exception ex)
            {
                Logger.ToError(ex);
            }

            return null;

        }

        /// <summary>
        /// Loads candles from the specified start date to the current moment.
        /// </summary>
        /// <param name="category">instrument type</param>
        /// <param name="symbol">instrument  symbol</param>
        /// <param name="interval">interval</param>
        /// <param name="startTime">start time</param>
        /// <returns></returns>
        public async Task<Candle[]> LoadCandlesHistory(Category category, string symbol, MarketInterval interval, DateTime startTime)
        {
            var temp = new List<Candle>();
            DateTime start = startTime;

            try
            {
                while (true)
                {
                    var items = await GetCandlesHistory(category, symbol, interval, start);

                    if (items == null || items.Length == 0) break;

                    temp.AddRange([.. items.OrderBy(f => f.OpenTime)]);

                    start = temp.Last().OpenTime.AddMinutes(int.Parse(interval));

                    if (start > DateTime.UtcNow) break;

                    if (items.Length < 1000) break;
                }

                // Последняя свеча при загрузке до текущей даты обычно незавершенная
                temp[^1] = temp.Last().Clone(false);

                return [.. temp];

            }
            catch (Exception ex)
            {
                Logger.ToError(ex);
            }

            return null;

        }

        #endregion

        #region fundingRate

        public async Task<List<FundingRate>> LoadFundingHistory(Category category, string symbol, DateTime startTime)
        {
            var rates = new List<FundingRate>();

            DateTime start = startTime;
            DateTime end = DateTime.UtcNow <= start.AddHours(8 * 199) ? DateTime.UtcNow : start.AddHours(8 * 199);

            try
            {
                while (true)
                {
                    var temp = await GetFundingHistory(category, symbol, start, end);
                    if (temp == null || temp.Count == 0) break;
                    rates.AddRange([.. temp.OrderBy(f => f.Time)]);
                    start = rates.Last().Time.AddHours(8);
                    if (start > DateTime.UtcNow) break;
                    end = end >= DateTime.UtcNow ? DateTime.UtcNow : start.AddHours(8 * 199);
                }

                return rates;

            }
            catch (Exception ex)
            {
                Logger.ToError(ex);
            }

            return null;

        }

        public async Task<List<FundingRate>> GetFundingHistory(Category category, string symbol, DateTime startTime, DateTime? endTime = null)
        {
            try
            {

                var fundResponse = await market.GetMarketFundingHistory(category: category,
                    symbol: symbol,
                    startTime: startTime.ToUnixTimeSeconds(),
                    endTime: endTime?.ToUnixTimeSeconds());

                FundingRate[]? temp = null;
                var tempResult = JsonConvert.DeserializeObject<BybitResponse>(fundResponse);
                if (tempResult.RetMsg == "OK")
                {
                    temp = JsonConvert.DeserializeObject<FundingRate[]>(tempResult.Result.List.ToString());
                }

                var fundings = temp.Select(t => new FundingRate { Timestamp = t.Timestamp, Symbol = t.Symbol, Rate = t.Rate * 100 });

                return [.. fundings];

            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }

            return null;

        }

        #endregion

        #region web-sokets

        /// <summary>
        /// Subscribe to receive candles via a web socket. A CandleUpdate event will be generated when received.
        /// </summary>
        /// <param name="symbols">Instruments list</param>
        /// <param name="frame">Interval</param>
        /// <param name="subscription">Subscription identificator</param>
        /// <returns></returns>
        public async Task<string> SubscribeCandles(string[] symbols, TimeFrames frame, string? subscription = null)
        {

            subscription ??= $"kline.{(int)frame}"; // if subscription == null

            if (Subscriptions.ContainsKey(subscription)) return subscription;

            candleTimes.Clear();

            var args = new List<string>();
            foreach (var symbol in symbols)
            {
                args.Add($"kline.{(int)frame}.{symbol}");
                candleTimes.Add(symbol, DateTime.MinValue);
            }

            var socket = new BybitLinearWebSocket();

            CancellationTokenSource source = new();
            CancellationToken token = source.Token;

            socket.OnMessageReceived(
                (data) =>
                {
                    try
                    {

                        if (data.Contains("topic") && data.Contains("type") && data.Contains("ts"))
                        {
                            var resp = JsonConvert.DeserializeObject<KlineResponse>(data);
                            var symbol = resp.topic.Split('.')[2];

                            // уменьшаем частоту событий
                            if (!resp.data.Last().confirm && (resp.data.Last().tstime - candleTimes[symbol]) < candleBrake)
                                return Task.CompletedTask;

                            var candles = new List<Candle>();

                            foreach (var kline in resp.data)
                            {
                                candles.Add(kline.ToCandle());
                            }

                            if (!resp.data.Last().confirm)
                                candleTimes[symbol] = resp.data.Last().tstime;

                            OnCandleUpdate(symbol, frame, candles);

                        }
                        else if (data.Contains("success") && data.Contains("conn_id") && data.Contains("op"))
                        {
                            var resp = JsonConvert.DeserializeObject<SoketSubscribeResponse>(data);

                            if (resp.success)
                            {
                                switch (resp.op)
                                {
                                    case "subscribe":
                                        {
                                            break;
                                        }
                                    case "ping":
                                        {
                                            break;
                                        }
                                    default:
                                        break;
                                }
                            }
                            else
                            {
                                throw new Exception(data);
                            }

                        }
                        else
                        {
                            throw new Exception(data);
                        }

                    }
                    catch (Exception ex)
                    {
                        Logger.ToError(ex);
                    }

                    return Task.CompletedTask;
                }, token);

            await socket.ConnectAsync([.. args], token);

            Subscriptions.Add(subscription, Tuple.Create(socket, token));

            return subscription;
        }

        /// <summary>
        /// Unsubscribes from a web socket channel.
        /// </summary>
        /// <param name="subscription">Subscription identificator</param>
        /// <returns></returns>
        public async Task Unsubscribe(string subscription)
        {
            if (Subscriptions.TryGetValue(subscription, out Tuple<BybitLinearWebSocket, CancellationToken>? value))
            {
                await value.Item1.DisconnectAsync(value.Item2);
                Subscriptions.Remove(subscription);
            }
        }

        #endregion

        //var privateWebsocket = new BybitPrivateWebsocket(apiKey: "xxxxxxxxx", apiSecret: "xxxxxxxxxx", useTestNet: true, debugMode: true);
        //privateWebsocket.OnMessageReceived(
        //    (data) =>
        //    {
        //        Console.WriteLine(data);
        //        return Task.CompletedTask;
        //    }, CancellationToken.None);
        //await privateWebsocket.ConnectAsync(new string[] { "order" }, CancellationToken.None);

        //BybitTradeService tradeService = new(apiKey: "xxxxxxxxxxxxxx", apiSecret: "xxxxxxxxxxxxxxxxxxxxx");
        //var orderInfo = await tradeService.PlaceOrder(category: Category.LINEAR, symbol: "BLZUSDT", side: Side.BUY, orderType: OrderType.MARKET, qty: "15", timeInForce: TimeInForce.GTC);
        //Console.WriteLine(orderInfo);

        //BybitAccountService accountService = new(apiKey: "xxxxxxxxxxxxxx", apiSecret: "xxxxxxxxxxxxxxxxxxxxx");
        //var accountInfo = await accountService.GetAccountBalance(accountType: AccountType.Unified);
        //Console.WriteLine(accountInfo);

        //BybitPositionService positionService = new(apiKey: "xxxxxxxxxxxxxx", apiSecret: "xxxxxxxxxxxxxxxxxxxxx", BybitConstants.HTTP_TESTNET_URL);
        //var positionInfo = await positionService.GetPositionInfo(category: Category.LINEAR, symbol: "BLZUSDT");
        //Console.WriteLine(positionInfo);
    }
}