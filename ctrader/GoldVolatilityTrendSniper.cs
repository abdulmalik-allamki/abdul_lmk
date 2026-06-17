using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    // ----------------------------------------------------------------------------------
    //  Gold Volatility Trend Sniper
    //  A mechanical XAU/USD swing/intraday cBot for the cTrader Automate (C#) platform.
    //
    //  Logic summary:
    //    * Evaluates ONCE per closed M15 bar (OnBar).
    //    * Trend filter   : price vs the 200 EMA.
    //    * Momentum filter : RSI(14) band (50-70 for longs, 30-50 for shorts).
    //    * Trigger        : a clean 20/50 EMA crossover on the just-closed bar.
    //    * Risk           : ATR-derived Stop Loss (ATR x 2) and Take Profit (ATR x 4) => 1:2 R:R.
    //    * Safety         : single position only, break-even move once price travels ATR x 1.5.
    //
    //  Attach this bot to an M15 XAU/USD chart. The class is fully self-contained and
    //  compiles in cTrader Automate without external references.
    // ----------------------------------------------------------------------------------
    [Robot(AccessRights = AccessRights.None, TimeZone = TimeZones.UTC)]
    public class GoldVolatilityTrendSniper : Robot
    {
        // ============================ UI PARAMETERS ============================
        [Parameter("Strategy Name", DefaultValue = "Gold Volatility Trend Sniper", Group = "General")]
        public string StrategyName { get; set; }

        [Parameter("Fast EMA Period", DefaultValue = 20, MinValue = 2, Group = "Indicators")]
        public int FastEmaPeriod { get; set; }

        [Parameter("Slow EMA Period", DefaultValue = 50, MinValue = 3, Group = "Indicators")]
        public int SlowEmaPeriod { get; set; }

        [Parameter("Trend Filter EMA Period", DefaultValue = 200, MinValue = 10, Group = "Indicators")]
        public int TrendEmaPeriod { get; set; }

        [Parameter("RSI Period", DefaultValue = 14, MinValue = 2, Group = "Indicators")]
        public int RsiPeriod { get; set; }

        [Parameter("ATR Period", DefaultValue = 14, MinValue = 2, Group = "Indicators")]
        public int AtrPeriod { get; set; }

        [Parameter("ATR Multiplier for Stop Loss", DefaultValue = 2.0, MinValue = 0.1, Step = 0.1, Group = "Risk")]
        public double AtrSlMultiplier { get; set; }

        [Parameter("ATR Multiplier for Take Profit", DefaultValue = 4.0, MinValue = 0.1, Step = 0.1, Group = "Risk")]
        public double AtrTpMultiplier { get; set; }

        [Parameter("ATR Multiplier for Break-Even", DefaultValue = 1.5, MinValue = 0.1, Step = 0.1, Group = "Risk")]
        public double AtrBreakEvenMultiplier { get; set; }

        [Parameter("Volume (Units)", DefaultValue = 10000, MinValue = 1, Group = "Risk")]
        public double VolumeInUnits { get; set; }

        [Parameter("Start Trading Hour (GMT)", DefaultValue = 7, MinValue = 0, MaxValue = 23, Group = "Session")]
        public int StartTradingHour { get; set; }

        [Parameter("End Trading Hour (GMT)", DefaultValue = 17, MinValue = 0, MaxValue = 24, Group = "Session")]
        public int EndTradingHour { get; set; }

        // ============================ INDICATORS / STATE ============================
        private ExponentialMovingAverage _fastEma;
        private ExponentialMovingAverage _slowEma;
        private ExponentialMovingAverage _trendEma;
        private RelativeStrengthIndex _rsi;
        private AverageTrueRange _atr;

        // A stable label so the bot only manages its own positions.
        private const string BotLabel = "GoldVolatilityTrendSniper";

        // ============================ LIFECYCLE ============================
        protected override void OnStart()
        {
            // Clean, defensive indicator initialisation on the chart's price series.
            _fastEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, FastEmaPeriod);
            _slowEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, SlowEmaPeriod);
            _trendEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, TrendEmaPeriod);
            _rsi = Indicators.RelativeStrengthIndex(Bars.ClosePrices, RsiPeriod);
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Exponential);

            if (Bars.TimeFrame != TimeFrame.Minute15)
                Print("WARNING: '{0}' is tuned for the M15 timeframe but is attached to {1}. Re-attach to M15 for intended behaviour.",
                    StrategyName, Bars.TimeFrame);

            if (EndTradingHour <= StartTradingHour)
                Print("WARNING: End Trading Hour ({0}) is not after Start Trading Hour ({1}); the session window may never open.",
                    EndTradingHour, StartTradingHour);

            Print("{0} started on {1} | SL={2}xATR  TP={3}xATR  BE={4}xATR  Session {5:00}:00-{6:00}:00 GMT",
                StrategyName, SymbolName, AtrSlMultiplier, AtrTpMultiplier, AtrBreakEvenMultiplier,
                StartTradingHour, EndTradingHour);
        }

        // Entry evaluation runs strictly on the close of each (M15) bar.
        protected override void OnBar()
        {
            // Ensure every indicator has enough history before we trust any reading.
            int requiredBars = Math.Max(TrendEmaPeriod, Math.Max(SlowEmaPeriod, Math.Max(RsiPeriod, AtrPeriod))) + 2;
            if (Bars.Count < requiredBars)
                return;

            // Only ever manage a single position for this symbol/strategy.
            if (GetOwnPosition() != null)
                return;

            // Respect the allowed GMT trading window for opening NEW positions.
            if (!IsWithinTradingWindow())
                return;

            EvaluateEntry();
        }

        // Break-even management is checked on every tick for responsiveness.
        protected override void OnTick()
        {
            ManageBreakEven();
        }

        // ============================ ENTRY LOGIC ============================
        private void EvaluateEntry()
        {
            // Index 1 = the just-closed bar; index 2 = the bar before it.
            double closedPrice = Bars.ClosePrices.Last(1);

            double fastNow = _fastEma.Result.Last(1);
            double fastPrev = _fastEma.Result.Last(2);
            double slowNow = _slowEma.Result.Last(1);
            double slowPrev = _slowEma.Result.Last(2);
            double trend = _trendEma.Result.Last(1);
            double rsi = _rsi.Result.Last(1);
            double atr = _atr.Result.Last(1);

            // Guard against an invalid / not-yet-formed ATR reading.
            if (double.IsNaN(atr) || atr <= 0)
            {
                Print("Skipping entry: ATR not ready (value={0}).", atr);
                return;
            }

            // Clean crossovers measured on closed-bar values only.
            bool crossedUp = fastPrev <= slowPrev && fastNow > slowNow;
            bool crossedDown = fastPrev >= slowPrev && fastNow < slowNow;

            // Trend + momentum filters.
            bool bullishContext = closedPrice > trend && rsi > 50 && rsi < 70;
            bool bearishContext = closedPrice < trend && rsi > 30 && rsi < 50;

            if (crossedUp && bullishContext)
                OpenTrade(TradeType.Buy, atr, rsi);
            else if (crossedDown && bearishContext)
                OpenTrade(TradeType.Sell, atr, rsi);
        }

        private void OpenTrade(TradeType tradeType, double atr, double rsi)
        {
            // Convert ATR-based price distances into pip distances for the order request.
            double slDistancePrice = atr * AtrSlMultiplier;
            double tpDistancePrice = atr * AtrTpMultiplier;

            double slPips = Math.Round(slDistancePrice / Symbol.PipSize, 1);
            double tpPips = Math.Round(tpDistancePrice / Symbol.PipSize, 1);

            if (slPips <= 0 || tpPips <= 0)
            {
                Print("Aborting {0}: computed non-positive SL/TP (SL={1} pips, TP={2} pips).", tradeType, slPips, tpPips);
                return;
            }

            double volume = Symbol.NormalizeVolumeInUnits(VolumeInUnits, RoundingMode.ToNearest);
            if (volume <= 0)
            {
                Print("Aborting {0}: normalized volume is zero (requested {1} units).", tradeType, VolumeInUnits);
                return;
            }

            // Wrap execution so FxPro errors, slippage or spread spikes are handled gracefully.
            TradeResult result = ExecuteMarketOrder(tradeType, SymbolName, volume, BotLabel, slPips, tpPips);

            if (result.IsSuccessful)
            {
                Position p = result.Position;
                Print("OPENED {0} {1} @ {2} | Vol {3} units | ATR {4:F2} | SL {5} pips | TP {6} pips | RSI {7:F1}",
                    tradeType, SymbolName, p.EntryPrice, volume, atr, slPips, tpPips, rsi);
            }
            else
            {
                // Common transient causes: NoMoneyToOpen, MarketClosed, off-quotes / spread spike.
                Print("ORDER FAILED ({0}) for {1} {2}. Spread now {3:F1} pips. No retry on this bar.",
                    result.Error, tradeType, SymbolName, Symbol.Spread / Symbol.PipSize);
            }
        }

        // ============================ BREAK-EVEN MANAGER ============================
        private void ManageBreakEven()
        {
            Position position = GetOwnPosition();
            if (position == null)
                return;

            double atr = _atr.Result.LastValue;
            if (double.IsNaN(atr) || atr <= 0)
                return;

            double triggerDistance = atr * AtrBreakEvenMultiplier;
            double onePip = Symbol.PipSize;

            if (position.TradeType == TradeType.Buy)
            {
                double targetStop = position.EntryPrice + onePip;
                double moved = Symbol.Bid - position.EntryPrice;

                // Move to break-even only once, and only if it actually tightens the stop.
                bool needsMove = position.StopLoss == null || position.StopLoss.Value < targetStop;
                if (moved >= triggerDistance && needsMove)
                    ApplyBreakEven(position, targetStop);
            }
            else // Sell
            {
                double targetStop = position.EntryPrice - onePip;
                double moved = position.EntryPrice - Symbol.Ask;

                bool needsMove = position.StopLoss == null || position.StopLoss.Value > targetStop;
                if (moved >= triggerDistance && needsMove)
                    ApplyBreakEven(position, targetStop);
            }
        }

        private void ApplyBreakEven(Position position, double targetStop)
        {
            double normalizedStop = Math.Round(targetStop, Symbol.Digits);
            TradeResult result = ModifyPosition(position, normalizedStop, position.TakeProfit);

            if (result.IsSuccessful)
                Print("BREAK-EVEN: {0} #{1} stop moved to {2} (entry {3}).",
                    position.TradeType, position.Id, normalizedStop, position.EntryPrice);
            else
                Print("BREAK-EVEN FAILED ({0}) for {1} #{2}. Will retry on next tick.",
                    result.Error, position.TradeType, position.Id);
        }

        // ============================ HELPERS ============================
        private Position GetOwnPosition()
        {
            return Positions.FirstOrDefault(p => p.SymbolName == SymbolName && p.Label == BotLabel);
        }

        private bool IsWithinTradingWindow()
        {
            // Server time is UTC because the [Robot] attribute pins TimeZone to UTC.
            int hour = Server.Time.Hour;
            return hour >= StartTradingHour && hour < EndTradingHour;
        }

        protected override void OnException(Exception exception)
        {
            // Last-resort guard so an unexpected runtime fault is logged rather than silently swallowed.
            Print("UNHANDLED EXCEPTION: {0}", exception.Message);
        }

        protected override void OnStop()
        {
            Print("{0} stopped.", StrategyName);
        }
    }
}
