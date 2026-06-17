using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    // ----------------------------------------------------------------------------------
    //  Gold Micro Scalper
    //  A high-frequency M1 scalping cBot for cTrader Automate (C#).
    //
    //  Idea:
    //    * Takes many small trades with a tight stop, aiming for a small profit each time.
    //    * Before every entry it CONFIRMS the market is genuinely moving that way:
    //        - M1 fast/slow EMA micro-trend (and the fast EMA must be sloping the right way)
    //        - a HIGHER timeframe (default M5) EMA trend filter, so it never scalps against
    //          the bigger move
    //        - an RSI momentum band (avoids buying blow-off tops / selling capitulations)
    //        - the just-closed M1 candle must close in the trade direction
    //    * It refuses to trade when the spread is too wide or volatility (ATR) is too low,
    //      the two conditions that quietly bleed scalpers in choppy markets.
    //
    //  Safety:
    //    * One position at a time (no stacking / grid).
    //    * Optional max trades per day and max daily loss cap.
    //    * Optional break-even move and trading session window.
    //
    //  Attach this bot to an M1 XAU/USD chart.
    //  NOTE on "pips": on XAU/USD a pip is 0.01, so e.g. 100 pips = a $1.00 price move.
    //  Tune the TP/SL/spread values to your symbol and your broker's typical spread.
    // ----------------------------------------------------------------------------------
    [Robot(AccessRights = AccessRights.None, TimeZone = TimeZones.UTC)]
    public class GoldMicroScalper : Robot
    {
        // ============================ GENERAL ============================
        [Parameter("Strategy Name", DefaultValue = "Gold Micro Scalper", Group = "General")]
        public string StrategyName { get; set; }

        [Parameter("Volume (Lots)", DefaultValue = 0.1, MinValue = 0.01, Step = 0.01, Group = "General")]
        public double VolumeInLots { get; set; }

        // ============================ SIGNAL / CONFIRMATION ============================
        [Parameter("Fast EMA Period", DefaultValue = 9, MinValue = 2, Group = "Signal")]
        public int FastEmaPeriod { get; set; }

        [Parameter("Slow EMA Period", DefaultValue = 21, MinValue = 3, Group = "Signal")]
        public int SlowEmaPeriod { get; set; }

        [Parameter("Higher Timeframe", DefaultValue = "Minute5", Group = "Signal")]
        public TimeFrame HigherTimeFrame { get; set; }

        [Parameter("Higher TF Trend EMA Period", DefaultValue = 50, MinValue = 5, Group = "Signal")]
        public int HtfTrendEmaPeriod { get; set; }

        [Parameter("RSI Period", DefaultValue = 14, MinValue = 2, Group = "Signal")]
        public int RsiPeriod { get; set; }

        [Parameter("RSI Buy Lower Bound", DefaultValue = 50, MinValue = 1, MaxValue = 99, Group = "Signal")]
        public double RsiBuyLower { get; set; }

        [Parameter("RSI Buy Upper Bound", DefaultValue = 75, MinValue = 1, MaxValue = 100, Group = "Signal")]
        public double RsiBuyUpper { get; set; }

        // ============================ RISK / TARGETS ============================
        [Parameter("Take Profit (Pips)", DefaultValue = 100, MinValue = 1, Group = "Risk")]
        public double TakeProfitPips { get; set; }

        [Parameter("Stop Loss (Pips)", DefaultValue = 80, MinValue = 1, Group = "Risk")]
        public double StopLossPips { get; set; }

        [Parameter("Max Spread (Pips)", DefaultValue = 50, MinValue = 0, Group = "Risk")]
        public double MaxSpreadPips { get; set; }

        [Parameter("Min ATR (Pips) to Trade", DefaultValue = 30, MinValue = 0, Group = "Risk")]
        public double MinAtrPips { get; set; }

        [Parameter("ATR Period", DefaultValue = 14, MinValue = 2, Group = "Risk")]
        public int AtrPeriod { get; set; }

        [Parameter("Use Break-Even", DefaultValue = true, Group = "Risk")]
        public bool UseBreakEven { get; set; }

        [Parameter("Break-Even Trigger (Pips)", DefaultValue = 50, MinValue = 1, Group = "Risk")]
        public double BreakEvenTriggerPips { get; set; }

        // ============================ THROTTLES / SESSION ============================
        [Parameter("Max Trades Per Day (0 = off)", DefaultValue = 100, MinValue = 0, Group = "Throttles")]
        public int MaxTradesPerDay { get; set; }

        [Parameter("Max Daily Loss (Account Ccy, 0 = off)", DefaultValue = 0, MinValue = 0, Group = "Throttles")]
        public double MaxDailyLoss { get; set; }

        [Parameter("Use Session Filter", DefaultValue = false, Group = "Throttles")]
        public bool UseSessionFilter { get; set; }

        [Parameter("Start Trading Hour (GMT)", DefaultValue = 7, MinValue = 0, MaxValue = 23, Group = "Throttles")]
        public int StartTradingHour { get; set; }

        [Parameter("End Trading Hour (GMT)", DefaultValue = 20, MinValue = 0, MaxValue = 24, Group = "Throttles")]
        public int EndTradingHour { get; set; }

        // ============================ INDICATORS / STATE ============================
        private ExponentialMovingAverage _fastEma;
        private ExponentialMovingAverage _slowEma;
        private RelativeStrengthIndex _rsi;
        private AverageTrueRange _atr;

        private Bars _htfBars;
        private ExponentialMovingAverage _htfEma;

        private const string BotLabel = "GoldMicroScalper";

        private DateTime _tradingDay;
        private int _tradesToday;
        private double _dayPnL;

        // ============================ LIFECYCLE ============================
        protected override void OnStart()
        {
            _fastEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, FastEmaPeriod);
            _slowEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, SlowEmaPeriod);
            _rsi = Indicators.RelativeStrengthIndex(Bars.ClosePrices, RsiPeriod);
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Exponential);

            // Higher-timeframe trend filter ("is the bigger picture going the same way?").
            _htfBars = MarketData.GetBars(HigherTimeFrame);
            _htfEma = Indicators.ExponentialMovingAverage(_htfBars.ClosePrices, HtfTrendEmaPeriod);

            _tradingDay = Server.Time.Date;
            _tradesToday = 0;
            _dayPnL = 0;

            // Keep the daily P/L tally in sync as our positions close.
            Positions.Closed += OnPositionClosed;

            if (Bars.TimeFrame != TimeFrame.Minute)
                Print("WARNING: '{0}' is designed for the M1 timeframe but is attached to {1}. Re-attach to M1.",
                    StrategyName, Bars.TimeFrame);

            Print("{0} started on {1} | TP {2}p / SL {3}p | HTF {4} EMA{5} | MaxSpread {6}p | MinATR {7}p",
                StrategyName, SymbolName, TakeProfitPips, StopLossPips, HigherTimeFrame, HtfTrendEmaPeriod,
                MaxSpreadPips, MinAtrPips);
        }

        protected override void OnStop()
        {
            Positions.Closed -= OnPositionClosed;
            Print("{0} stopped. Trades today: {1}, day P/L: {2:F2}", StrategyName, _tradesToday, _dayPnL);
        }

        // Entry decisions are made once per closed M1 bar.
        protected override void OnBar()
        {
            ResetDailyCountersIfNeeded();

            int requiredBars = Math.Max(SlowEmaPeriod, Math.Max(RsiPeriod, AtrPeriod)) + 2;
            if (Bars.Count < requiredBars || _htfBars.Count < HtfTrendEmaPeriod + 2)
                return;

            // One position at a time.
            if (GetOwnPosition() != null)
                return;

            if (!DailyLimitsAllowTrading())
                return;

            if (UseSessionFilter && !IsWithinTradingWindow())
                return;

            // Spread guard — skip when execution cost is too high for a small scalp.
            double spreadPips = Symbol.Spread / Symbol.PipSize;
            if (spreadPips > MaxSpreadPips)
                return;

            // Volatility guard — don't scalp a dead, ranging market.
            double atrPips = _atr.Result.LastValue / Symbol.PipSize;
            if (double.IsNaN(atrPips) || atrPips < MinAtrPips)
                return;

            TradeType? signal = GetSignal();
            if (signal.HasValue)
                OpenScalp(signal.Value, spreadPips, atrPips);
        }

        protected override void OnTick()
        {
            if (UseBreakEven)
                ManageBreakEven();
        }

        // ============================ SIGNAL ============================
        private TradeType? GetSignal()
        {
            // Index 1 = the just-closed M1 bar.
            double close = Bars.ClosePrices.Last(1);
            double open = Bars.OpenPrices.Last(1);

            double fastNow = _fastEma.Result.Last(1);
            double fastPrev = _fastEma.Result.Last(2);
            double slowNow = _slowEma.Result.Last(1);
            double rsi = _rsi.Result.Last(1);

            // Higher-timeframe trend (closed HTF bar).
            double htfClose = _htfBars.ClosePrices.Last(1);
            double htfEmaNow = _htfEma.Result.Last(1);
            double htfEmaPrev = _htfEma.Result.Last(2);

            bool htfUp = htfClose > htfEmaNow && htfEmaNow > htfEmaPrev;
            bool htfDown = htfClose < htfEmaNow && htfEmaNow < htfEmaPrev;

            // ---- BUY: everything must agree the market is pushing up ----
            bool buyMicroTrend = fastNow > slowNow && fastNow > fastPrev;   // fast above slow AND rising
            bool buyCandle = close > open && close > fastNow;               // bullish close, above the fast EMA
            bool buyMomentum = rsi > RsiBuyLower && rsi < RsiBuyUpper;      // momentum present, not overbought
            if (htfUp && buyMicroTrend && buyCandle && buyMomentum)
                return TradeType.Buy;

            // ---- SELL: mirror image (RSI band reflected around 50) ----
            double rsiSellUpper = 100 - RsiBuyLower; // e.g. 50
            double rsiSellLower = 100 - RsiBuyUpper; // e.g. 25
            bool sellMicroTrend = fastNow < slowNow && fastNow < fastPrev;
            bool sellCandle = close < open && close < fastNow;
            bool sellMomentum = rsi < rsiSellUpper && rsi > rsiSellLower;
            if (htfDown && sellMicroTrend && sellCandle && sellMomentum)
                return TradeType.Sell;

            return null;
        }

        private void OpenScalp(TradeType tradeType, double spreadPips, double atrPips)
        {
            // Convert the requested lot size into the symbol's native volume units.
            double volume = Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(VolumeInLots), RoundingMode.ToNearest);
            if (volume <= 0)
            {
                Print("Aborting {0}: normalized volume is zero (requested {1} lots).", tradeType, VolumeInLots);
                return;
            }

            // Wrap execution so broker errors / spread spikes are handled gracefully.
            TradeResult result = ExecuteMarketOrder(tradeType, SymbolName, volume, BotLabel, StopLossPips, TakeProfitPips);

            if (result.IsSuccessful)
            {
                _tradesToday++;
                Position p = result.Position;
                Print("OPENED {0} {1} @ {2} | #{3} of day | Spread {4:F1}p | ATR {5:F1}p | TP {6}p / SL {7}p",
                    tradeType, SymbolName, p.EntryPrice, _tradesToday, spreadPips, atrPips, TakeProfitPips, StopLossPips);
            }
            else
            {
                Print("ORDER FAILED ({0}) for {1} {2}. Spread {3:F1}p. Skipping this bar.",
                    result.Error, tradeType, SymbolName, spreadPips);
            }
        }

        // ============================ BREAK-EVEN ============================
        private void ManageBreakEven()
        {
            Position position = GetOwnPosition();
            if (position == null)
                return;

            double triggerDistance = BreakEvenTriggerPips * Symbol.PipSize;
            double onePip = Symbol.PipSize;

            if (position.TradeType == TradeType.Buy)
            {
                double targetStop = position.EntryPrice + onePip;
                double moved = Symbol.Bid - position.EntryPrice;
                bool needsMove = position.StopLoss == null || position.StopLoss.Value < targetStop;
                if (moved >= triggerDistance && needsMove)
                    ApplyBreakEven(position, targetStop);
            }
            else
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
            TradeResult result = ModifyPosition(position, normalizedStop, position.TakeProfit, ProtectionType.Absolute);
            if (result.IsSuccessful)
                Print("BREAK-EVEN: {0} #{1} stop -> {2}", position.TradeType, position.Id, normalizedStop);
            else
                Print("BREAK-EVEN FAILED ({0}) for #{1}. Will retry next tick.", result.Error, position.Id);
        }

        // ============================ THROTTLES / HELPERS ============================
        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            if (args.Position.Label != BotLabel || args.Position.SymbolName != SymbolName)
                return;

            _dayPnL += args.Position.NetProfit;
            Print("CLOSED {0} #{1} | Net {2:F2} | Reason {3} | Day P/L {4:F2}",
                args.Position.TradeType, args.Position.Id, args.Position.NetProfit, args.Reason, _dayPnL);
        }

        private bool DailyLimitsAllowTrading()
        {
            if (MaxTradesPerDay > 0 && _tradesToday >= MaxTradesPerDay)
                return false;

            if (MaxDailyLoss > 0 && _dayPnL <= -MaxDailyLoss)
                return false;

            return true;
        }

        private void ResetDailyCountersIfNeeded()
        {
            if (Server.Time.Date != _tradingDay)
            {
                _tradingDay = Server.Time.Date;
                _tradesToday = 0;
                _dayPnL = 0;
                Print("New trading day {0:yyyy-MM-dd}: counters reset.", _tradingDay);
            }
        }

        private Position GetOwnPosition()
        {
            return Positions.FirstOrDefault(p => p.SymbolName == SymbolName && p.Label == BotLabel);
        }

        private bool IsWithinTradingWindow()
        {
            int hour = Server.Time.Hour;
            return hour >= StartTradingHour && hour < EndTradingHour;
        }

        protected override void OnException(Exception exception)
        {
            Print("UNHANDLED EXCEPTION: {0}", exception.Message);
        }
    }
}
