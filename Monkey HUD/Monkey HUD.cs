// ****************************************************************************
// MONKEY HUD
//             
// Change History
// Date      Name                  Desc
// ---------------------------------------------
// 20190310  Justin Bannerman      Orig
// 20220618  Justin Bannerman      Version 3
// ****************************************************************************
using System;
using System.Linq;
using System.Globalization;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;

namespace cAlgo
{
    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class MonkeyHUD : Indicator
    {
        [Parameter("ATR Period", DefaultValue = 14)]
        public int Period { get; set; }

        [Parameter("EMA Periods for overview", DefaultValue = 50)]
        public int EMAPeriods { get; set; }

        [Parameter("Show BUY/SELL bias", DefaultValue = false)]
        public bool ShowBuySell { get; set; }

        [Parameter("Status Font Size", DefaultValue = 16, MinValue = 8, MaxValue = 22, Step = 2)]
        public int StatusFontSize { get; set; }

        [Parameter("Status y pos ATRs", DefaultValue = 1.25, MinValue = 0, MaxValue = 3, Step = 0.1)]
        public double StatusyPosATRs { get; set; }

        [Parameter("Show candle patterns", DefaultValue = false)]
        public bool ShowPatterns { get; set; }

        [Parameter("Death screen of excessive risk", DefaultValue = true)]
        public bool DeathScreen { get; set; }

        [Parameter("Max Risk", DefaultValue = 2.0, Step = 0.1)]
        public double MaxRisk { get; set; }

        public Color mainTextColor = Color.DarkGray;
        private AverageTrueRange atr;
        private double ATRPips, spread, bal;
        private double dailyStartingBalance, dailyProfitLossPerc, dailyprofitLossAmt;
        private IAccount account;
        private MovingAverage EMAW1, EMAD1, EMAH8, EMAH4, EMAH1, EMAM15, TrendMA;
        private AverageDirectionalMovementIndexRating ADX;
        private readonly string uparrow = "▲";
        private readonly string downarrow = "▼";
        private const int adxPeriods = 13;

        protected override void Initialize()
        {
            account = this.Account;

            // Set up Data series & ATRs
            atr = Indicators.AverageTrueRange(Period, MovingAverageType.Simple);
            EMAW1 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Weekly).ClosePrices, EMAPeriods);
            EMAD1 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Daily).ClosePrices, EMAPeriods);
            EMAH8 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Hour8).ClosePrices, EMAPeriods);
            EMAH4 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Hour4).ClosePrices, EMAPeriods);
            EMAH1 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Hour).ClosePrices, EMAPeriods);
            EMAM15 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Minute15).ClosePrices, EMAPeriods);
            TrendMA = Indicators.MovingAverage(Bars.ClosePrices, 200, MovingAverageType.Exponential);
            ADX = Indicators.AverageDirectionalMovementIndexRating(adxPeriods);

            // Find the account balance at the end of previous day (23:59:59)
            DateTime yesterday = Time.AddDays(-1).Date;
            TimeSpan ts = new TimeSpan(23, 59, 59);
            yesterday = yesterday.Date + ts;
            HistoricalTrade trade = History.LastOrDefault(x => x.ClosingTime <= yesterday);
            dailyStartingBalance = trade != null ? trade.Balance : Account.Balance;

            dailyprofitLossAmt = 0;
            dailyProfitLossPerc = 0;
        }

        public override void Calculate(int index)
        {
            Color posColor = Color.LimeGreen;
            double totNetPL = 0.0, yPos, totPips = 0.0, numPos = 0;
            DateTime dt = DateTime.Now;
            string timeOpen = "";

            bal = account.Balance;


            // Get Open positions
            foreach (var position in Positions)
            {
                if (position.SymbolName.Equals(Symbol.Name.ToString()))
                {
                    numPos++;
                    totPips += Math.Abs((double)Symbol.Ask - position.EntryPrice) / Symbol.PipSize;
                    if (position.EntryTime < dt)
                        dt = position.EntryTime.ToLocalTime();
                }
            }

            yPos = Bars.HighPrices.LastValue; // + (atr.Result.LastValue * StatusyPosATRs);
            ChartText ct = Chart.DrawText("ct", "", Chart.LastVisibleBarIndex + 1, yPos, posColor);
            if (numPos > 0)
            {
                totPips = totPips / numPos;
                TimeSpan ts = DateTime.Now.Subtract(dt);
                timeOpen = ts.Hours.ToString("00") + ":" + ts.Minutes.ToString("00") + ":" + ts.Seconds.ToString("00");

                // Display NetPL % on open trades. 
                totNetPL = Symbol.UnrealizedNetProfit / bal;
                if (totNetPL == 0)
                    posColor = Color.White;
                if (totNetPL < 0)
                    posColor = Color.OrangeRed;

                //ct = Chart.DrawText("ct", totNetPL.ToString("+0.000%;-0.000%;0.000%") + "\n" + totPips.ToString("0.0") + " Pips\n" + timeOpen, Chart.LastVisibleBarIndex + 1, yPos, posColor);
                ct = Chart.DrawText("ct", totNetPL.ToString("+0.000%;-0.000%;0.000%") + "\n" + timeOpen, Chart.LastVisibleBarIndex + 1, yPos, posColor);
                ct.FontSize = StatusFontSize;
                if (totNetPL < (MaxRisk * -1))
                {
                    Chart.ColorSettings.BackgroundColor = Color.Red;
                }
                else
                {
                    Chart.ColorSettings.BackgroundColor = Color.FromHex("FF141417");
                }
            }
            ct.HorizontalAlignment = HorizontalAlignment.Right;
            ct.VerticalAlignment = VerticalAlignment.Top;

            ATRPips = atr.Result.LastValue / Symbol.PipSize;
            spread = Symbol.Spread / Symbol.PipSize;
            dailyprofitLossAmt = Account.Balance - dailyStartingBalance;
            dailyProfitLossPerc = (Account.Balance - dailyStartingBalance) / dailyStartingBalance * 100;

            // EMA stuff
            int upCount = 0, downCount = 0;
            string EMAText = "W1";
            string direction = "x";
            if (Bars.LowPrices.LastValue > EMAW1.Result.LastValue)
            {
                direction = uparrow;
                upCount++;
            }
            if (Bars.HighPrices.LastValue < EMAW1.Result.LastValue)
            {
                direction = downarrow;
                downCount++;
            }
            EMAText += direction + " D1";
            direction = "x";
            if (Bars.LowPrices.LastValue > EMAD1.Result.LastValue)
            {
                direction = uparrow;
                upCount++;
            }
            if (Bars.HighPrices.LastValue < EMAD1.Result.LastValue)
            {
                direction = downarrow;
                downCount++;
            }
            EMAText += direction + " H8";
            direction = "x";
            if (Bars.LowPrices.LastValue > EMAH8.Result.LastValue)
            {
                direction = uparrow;
                upCount++;
            }
            if (Bars.HighPrices.LastValue < EMAH8.Result.LastValue)
            {
                direction = downarrow;
                downCount++;
            }
            EMAText += direction + " H4";
            direction = "x";
            if (Bars.LowPrices.LastValue > EMAH4.Result.LastValue)
            {
                direction = uparrow;
                upCount++;
            }
            if (Bars.HighPrices.LastValue < EMAH4.Result.LastValue)
            {
                direction = downarrow;
                downCount++;
            }
            EMAText += direction + " H1";
            direction = "x";
            if (Bars.LowPrices.LastValue > EMAH1.Result.LastValue)
            {
                direction = uparrow;
                upCount++;
            }
            if (Bars.HighPrices.LastValue < EMAH1.Result.LastValue)
            {
                direction = downarrow;
                downCount++;
            }
            EMAText += direction + " M15";
            direction = "x";
            if (Bars.LowPrices.LastValue > EMAM15.Result.LastValue)
            {
                direction = uparrow;
                upCount++;
            }
            if (Bars.HighPrices.LastValue < EMAM15.Result.LastValue)
            {
                direction = downarrow;
                downCount++;
            }
            EMAText += direction;
            Color actionColor = Color.Bisque;
            string actionText = "No Trend consensus";
            if (upCount > 3)
            {
                actionText = "=BUY=";
                actionColor = Color.LimeGreen;
            }
            if (downCount > 3)
            {
                actionText = "=SELL=";
                actionColor = Color.OrangeRed;
            }


            // Top Centre, display Stats & EMA
            Chart.DrawStaticText("PROF", "Daily " + dailyProfitLossPerc.ToString("+0.00;-0.00;0.00") + "%     ATR " + ATRPips.ToString("0.0") + " Pips     ADX " + ADX.ADX.LastValue.ToString("0") + "     Spread " + spread.ToString("0.0") + " Pips", VerticalAlignment.Top, HorizontalAlignment.Center, mainTextColor);
            Chart.DrawStaticText("DIR", "\n" + "EMA" + EMAPeriods.ToString() + "   " + EMAText, VerticalAlignment.Top, HorizontalAlignment.Center, mainTextColor);
            if (ShowBuySell)
                Chart.DrawStaticText("ACT", "\n\n" + actionText, VerticalAlignment.Top, HorizontalAlignment.Center, actionColor);



            var arrowName = string.Format("arrow {0}", index);
            var reversalName = string.Format("reversal {0}", index);

            if (ShowPatterns)
            {
                // Check Bullish Engulfing
                //if (BullishEngulfingCandle(index) && Bars.ClosePrices[index] > TrendMa.Result[index])
                if (BullishEngulfingCandle(index))
                    Chart.DrawIcon(arrowName, ChartIconType.UpArrow, index, (Bars.LowPrices[index] - 0.0002), Color.LimeGreen);

                // Check Bearish Engulfing
                //if (BearishEngulfingCandle(index) && Bars.ClosePrices[index] < TrendMa.Result[index])
                if (BearishEngulfingCandle(index))
                    Chart.DrawIcon(arrowName, ChartIconType.DownArrow, index, (Bars.HighPrices[index] + 0.0002), Color.Salmon);

                // Check reversals
                if (EveningStarReversalPattern(index))
                    // && Bars.ClosePrices[index] > TrendMA.Result[index])
                    Chart.DrawIcon(reversalName, ChartIconType.Star, index - 1, (Bars.HighPrices[index - 1] + 0.0004), Color.Cyan);
                if (MorningStarReversalPattern(index))
                    // && Bars.ClosePrices[index] < TrendMA.Result[index])
                    Chart.DrawIcon(reversalName, ChartIconType.Star, index - 1, (Bars.LowPrices[index - 1] - 0.0004), Color.Cyan);

            }

        }


        // ==============================
        // Candle identification routines
        // ==============================
        public bool BullishEngulfingCandle(int index)
        {
            bool result = false;
            if (Bars.OpenPrices[index - 1] > Bars.ClosePrices[index - 1] && Bars.OpenPrices[index] < Bars.ClosePrices[index] && Bars.OpenPrices[index] <= Bars.ClosePrices[index - 1] && Bars.ClosePrices[index] > Bars.OpenPrices[index - 1])
                result = true;
            return result;
        }

        public bool BearishEngulfingCandle(int index)
        {
            bool result = false;
            if (Bars.OpenPrices[index - 1] < Bars.ClosePrices[index - 1] && Bars.OpenPrices[index] > Bars.ClosePrices[index] && Bars.OpenPrices[index] >= Bars.ClosePrices[index - 1] && Bars.ClosePrices[index] < Bars.OpenPrices[index - 1])
                result = true;
            return result;
        }

        public bool EveningStarReversalPattern(int index)
        {
            // For small body candle, assume < 1/4 of candle is body
            bool result = false;
            if (Bars.OpenPrices[index - 2] < Bars.ClosePrices[index - 2] && Bars.HighPrices[index - 2] < Bars.HighPrices[index - 1] && Bars.HighPrices[index - 1] > Bars.HighPrices[index] && (Math.Abs(Bars.OpenPrices[index - 1] - Bars.ClosePrices[index - 1]) / (Bars.HighPrices[index - 1] - Bars.LowPrices[index - 1])) < 0.25 && Bars.OpenPrices[index] > Bars.ClosePrices[index])
                result = true;
            return result;
        }

        public bool MorningStarReversalPattern(int index)
        {
            // For small body candle, assume < 1/4 of candle is body
            bool result = false;
            if (Bars.OpenPrices[index - 2] > Bars.ClosePrices[index - 2] && Bars.LowPrices[index - 2] > Bars.LowPrices[index - 1] && Bars.LowPrices[index - 1] < Bars.LowPrices[index] && (Math.Abs(Bars.OpenPrices[index - 1] - Bars.ClosePrices[index - 1]) / (Bars.HighPrices[index - 1] - Bars.LowPrices[index - 1])) < 0.25 && Bars.OpenPrices[index] < Bars.ClosePrices[index])
                result = true;
            return result;
        }


        public bool Bullish3LineStrike(int index, double minSize)
        {
            bool result = false;
            if (Bars.OpenPrices[index] > Bars.ClosePrices[index] && Bars.OpenPrices[index - 1] < Bars.ClosePrices[index - 1] && Bars.OpenPrices[index - 2] < Bars.ClosePrices[index - 2] && Bars.OpenPrices[index - 3] < Bars.ClosePrices[index - 3] && Bars.ClosePrices[index - 3] < Bars.ClosePrices[index - 2] && Bars.ClosePrices[index - 2] < Bars.ClosePrices[index - 1] && Bars.ClosePrices[index - 1] <= Bars.OpenPrices[index] && Bars.OpenPrices[index - 3] >= Bars.ClosePrices[index])
                if (Math.Abs(Bars.HighPrices[index - 3] - Bars.LowPrices[index - 3]) > minSize && Math.Abs(Bars.HighPrices[index - 2] - Bars.LowPrices[index - 2]) > minSize && Math.Abs(Bars.HighPrices[index - 1] - Bars.LowPrices[index - 1]) > minSize)
                    result = true;
            return result;
        }


        public bool Bearish3LineStrike(int index, double minSize)
        {
            bool result = false;
            if (Bars.OpenPrices[index] < Bars.ClosePrices[index] && Bars.OpenPrices[index - 1] > Bars.ClosePrices[index - 1] && Bars.OpenPrices[index - 2] > Bars.ClosePrices[index - 2] && Bars.OpenPrices[index - 3] > Bars.ClosePrices[index - 3] && Bars.ClosePrices[index - 3] < Bars.ClosePrices[index - 2] && Bars.ClosePrices[index - 2] < Bars.ClosePrices[index - 1] && Bars.ClosePrices[index - 1] >= Bars.OpenPrices[index] && Bars.OpenPrices[index - 3] <= Bars.ClosePrices[index])
                if (Math.Abs(Bars.HighPrices[index - 3] - Bars.LowPrices[index - 3]) > minSize && Math.Abs(Bars.HighPrices[index - 2] - Bars.LowPrices[index - 2]) > minSize && Math.Abs(Bars.HighPrices[index - 1] - Bars.LowPrices[index - 1]) > minSize)
                    result = true;
            return result;
        }

    }
}
