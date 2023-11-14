// ****************************************************************************
// MONKEY HUD
//             
// Change History
// Date      Name                  Desc
// ---------------------------------------------
// 20190310  Justin Bannerman      Orig
// 20220618  Justin Bannerman      Version 3
// 20230119  Justin Bannerman      Version 4 - add deal map
// ****************************************************************************
using System;
using System.Linq;
using System.Globalization;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;
using System.Reflection.PortableExecutable;
using System.ComponentModel;
using System.Diagnostics;

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

        [Parameter("Text Colour", DefaultValue = "DarkGray")]
        public string MainTextColor { get; set; }

        [Parameter("Status Font Size", DefaultValue = 16, MinValue = 8, MaxValue = 22, Step = 2)]
        public int StatusFontSize { get; set; }

        [Parameter("Status y pos ATRs", DefaultValue = 1.25, MinValue = 0, MaxValue = 3, Step = 0.1)]
        public double StatusyPosATRs { get; set; }

        [Parameter("Daily Cutover Hour", DefaultValue = 1, MinValue = 0, MaxValue = 24)]
        public int DailyCutoverHour { get; set; }

        //[Parameter("Show Daily Bias", DefaultValue = false)]
        //public bool ShowDailyBias { get; set; }

        //[Parameter("Show candle patterns", DefaultValue = false)]
        //public bool ShowPatterns { get; set; }

        [Parameter("Show deal map", DefaultValue = true)]
        public bool ShowDealMap { get; set; }

        private AverageTrueRange atr;
        private Color mainTextColor;
        private double ATRPips, spread, bal;
        private double dailyStartingBalance, dailyProfitLossPerc, dailyprofitLossAmt;
        private IAccount account;
        private MovingAverage EMAW1, EMAD1, EMAH8, EMAH4, EMAH1, EMAM15, EMAM5, TrendMA;
        private AverageDirectionalMovementIndexRating ADX;
        private readonly string uparrow = "▲";
        private readonly string downarrow = "▼";
        private const int adxPeriods = 13;
        private DateTime balDate;
        public string BotVersion = "";
        public const string Expirydate = "31/12/2023";
        public bool _enabled = true;

        protected override void Initialize()
        {
            Version version = this.Application.Version;
            BotVersion = String.Format("Version: {0}.{1}.{2}.{3}", version.Major, version.Minor, version.Build, version.Revision);
            Print(BotVersion + " (Expires " + Expirydate + ")");
            if (DateTime.Now.Date > DateTime.Parse(Expirydate))
            {
                Print("This version of the HUD has expired. Please update to the latest version.");
                _enabled = false;
                return;
            }

            account = this.Account;
            double PDH = MarketData.GetBars(TimeFrame.Daily).Last(1).High;
            double PPDH = MarketData.GetBars(TimeFrame.Daily).Last(2).High;
            double PDL = MarketData.GetBars(TimeFrame.Daily).Last(1).Low;
            double PPDL = MarketData.GetBars(TimeFrame.Daily).Last(2).Low;

            // Set up Data series & ATRs
            atr = Indicators.AverageTrueRange(Period, MovingAverageType.Simple);
            EMAW1 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Weekly).ClosePrices, EMAPeriods);
            EMAD1 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Daily).ClosePrices, EMAPeriods);
            EMAH8 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Hour8).ClosePrices, EMAPeriods);
            EMAH4 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Hour4).ClosePrices, EMAPeriods);
            EMAH1 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Hour).ClosePrices, EMAPeriods);
            EMAM15 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Minute15).ClosePrices, EMAPeriods);
            EMAM5 = Indicators.ExponentialMovingAverage(MarketData.GetBars(TimeFrame.Minute5).ClosePrices, EMAPeriods);
            TrendMA = Indicators.MovingAverage(Bars.ClosePrices, 200, MovingAverageType.Exponential);
            ADX = Indicators.AverageDirectionalMovementIndexRating(adxPeriods);

            // Find the account balance at 01:00:00am
            DateTime startOfToday = Time.ToLocalTime().Date;
            TimeSpan ts = new TimeSpan(DailyCutoverHour, 0, 0);
            startOfToday = startOfToday.Date + ts;
            DateTime tomorrow = startOfToday.AddDays(1);
            Print("Start of day - LocalTime {0}", startOfToday);

            dailyStartingBalance = Account.Balance;
            double prevBal = 0;
            double todayGain = 0;
            int cnt = 0;

            foreach (var t in History.OrderByDescending(x => x.ClosingTime.ToLocalTime()))
            {
                // Check if trade is before start of day
                if (t.ClosingTime.ToLocalTime() < startOfToday)
                {
                    dailyStartingBalance = t.Balance;
                    prevBal = t.Balance;
                    Print("Yesterday's Last Trade {0}, resulting balance {1}", t.ClosingTime.ToLocalTime(), t.Balance);
                    break;
                }
                // Accumulate todays trades
                todayGain += t.NetProfit;
                cnt++;
                Print("Todays trade #{3}: {0} gain {1} cumulative gain {2}", t.ClosingTime.ToLocalTime(), t.NetProfit, todayGain,cnt);
            }
            double depositWihdrawals = Account.Balance - todayGain - prevBal;
            Print("DepositWithdraw {0} = Balance {1} - TodayGain {2} - PrevBal {3}", depositWihdrawals, Account.Balance, todayGain, prevBal);
            dailyStartingBalance += depositWihdrawals;
            Print("Daily Starting Balance = {0}", dailyStartingBalance);


            dailyprofitLossAmt = 0;
            dailyProfitLossPerc = 0;
            mainTextColor = Color.FromName(MainTextColor);
            if (mainTextColor == Color.Empty) mainTextColor = Chart.ColorSettings.ForegroundColor;
            //if (ShowDailyBias)
            //{
            //    Chart.DrawHorizontalLine("PrevDailyHigh", PDH, Color.LightSeaGreen,2);
            //    Chart.DrawHorizontalLine("PrevDailyLow", PDL, Color.LightSalmon, 2);
            //    //Chart.DrawHorizontalLine("PrevPrevDailyHigh", PPDH, Color.LightSeaGreen, 1,LineStyle.DotsRare);
            //    //Chart.DrawHorizontalLine("PrevPrevDailyLow", PPDL, Color.LightSalmon, 1, LineStyle.DotsRare);
            //}
        }

        public override void Calculate(int index)
        {

            if (!_enabled) return;

            Color posColor = Color.LimeGreen;
            double totNetPL = 0.0, yPos, totPips = 0.0, numPos = 0;
            DateTime dt = DateTime.Now;
            string timeOpen = "";

            bal = account.Balance;

            // Remove any old deal map lines
            foreach (var obj in Chart.Objects)
            {
                if (obj == null) continue;
                if (obj.Name.Contains("dm_pos_"))
                    Chart.RemoveObject(obj.Name);
            }

            // Get Open positions
            foreach (var position in Positions)
            {
                if (position.SymbolName.Equals(Symbol.Name.ToString()))
                {
                    numPos++;
                    totPips += position.Pips;
                    if (position.EntryTime < dt)
                        dt = position.EntryTime.ToLocalTime();
                    Color dmLineColor = Color.LimeGreen;
                    if (position.NetProfit < 0) dmLineColor = Color.OrangeRed;
                    if (ShowDealMap)
                    {
                        Chart.DrawTrendLine("dm_pos_" + numPos.ToString(), position.EntryTime, position.EntryPrice, Bars.LastBar.OpenTime, Bars.ClosePrices.Last(), dmLineColor, 1);
                    }
                }
            }

            yPos = Bars.HighPrices.LastValue; // + (atr.Result.LastValue * StatusyPosATRs);
            ChartText ct = Chart.DrawText("ct", "", Chart.LastVisibleBarIndex + 1, yPos, posColor);
            if (numPos > 0)
            {
                totPips = totPips / numPos;
                TimeSpan tt = DateTime.Now.Subtract(dt);
                timeOpen = tt.Hours.ToString("00") + ":" + tt.Minutes.ToString("00") + ":" + tt.Seconds.ToString("00");

                // Display NetPL % on open trades. 
                totNetPL = Symbol.UnrealizedNetProfit / bal;
                if (totNetPL == 0)
                    posColor = Color.White;
                if (totNetPL < 0)
                    posColor = Color.OrangeRed;

                ct = Chart.DrawText("ct", totNetPL.ToString("+0.000%;-0.000%;0.000%") + "\n" + totPips.ToString("0.0") + " Pips\n" + timeOpen, Chart.LastVisibleBarIndex + 1, yPos, posColor);
                ct.FontSize = StatusFontSize;
            }
            ct.HorizontalAlignment = HorizontalAlignment.Right;
            ct.VerticalAlignment = VerticalAlignment.Top;

            ATRPips = atr.Result.LastValue / Symbol.PipSize;
            spread = Symbol.Spread / Symbol.PipSize;

            // Find the account balance at 01:00:00am
            //DateTime today = Time.ToLocalTime().Date;
            //TimeSpan ts = new TimeSpan(DailyCutoverHour, 0, 0);
            //today = today.Date + ts;
            //DateTime tomorrow = today.AddDays(1);
            //var todaysTrades = History.OrderBy(x => x.ClosingTime.ToLocalTime() >= today && x.ClosingTime.ToLocalTime() <= tomorrow);
            //double todaysGains = 0;
            //foreach (HistoricalTrade historicalTrade in todaysTrades)
            //{
            //    todaysGains += historicalTrade.NetProfit;
            //}

            //HistoricalTrade trade = History.LastOrDefault(x => x.ClosingTime.ToLocalTime() <= today);
            //dailyStartingBalance = trade != null ? trade.Balance : Account.Balance;

            //HistoricalTrade tradeB = History.LastOrDefault();

            dailyprofitLossAmt = Account.Balance - dailyStartingBalance;
            dailyProfitLossPerc = (Account.Balance - dailyStartingBalance) / dailyStartingBalance * 100;

            // EMA stuff
            int upCount = 0, downCount = 0;
            string EMAText = "W1";
            string direction = "x";
            if (Bars.LowPrices.LastValue > EMAW1.Result.LastValue)
            {
                direction = uparrow;
                //upCount++;
            }
            if (Bars.HighPrices.LastValue < EMAW1.Result.LastValue)
            {
                direction = downarrow;
                //downCount++;
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
            EMAText += direction + " M5";
            direction = "x";
            if (Bars.LowPrices.LastValue > EMAM5.Result.LastValue)
            {
                direction = uparrow;
                upCount++;
            }
            if (Bars.HighPrices.LastValue < EMAM5.Result.LastValue)
            {
                direction = downarrow;
                downCount++;
            }
            EMAText += direction;
            Color actionColor = Color.Bisque;
            string actionText = "No Trend consensus";
            if (upCount >= 3 && upCount > downCount)
            {
                actionText = "=Look for BUY=";
                actionColor = Color.LimeGreen;
            }
            if (downCount >= 3 && upCount < downCount)
            {
                actionText = "=Look for SELL=";
                actionColor = Color.OrangeRed;
            }
            if (Chart.TimeFrame <= TimeFrame.Minute15)
            {
                if (Bars.HighPrices.LastValue < EMAH1.Result.LastValue && Bars.HighPrices.LastValue < EMAH4.Result.LastValue)
                {
                    actionText = "=On " + Chart.TimeFrame.ShortName + " Look for SELL=";
                    actionColor = Color.OrangeRed;
                }
                if (Bars.LowPrices.LastValue > EMAH1.Result.LastValue && Bars.LowPrices.LastValue > EMAH4.Result.LastValue)
                {
                    actionText = "=On " + Chart.TimeFrame.ShortName + " Look for BUY=";
                    actionColor = Color.LimeGreen;
                }
            }


            // Top Centre, display Stats & EMA
            Chart.DrawStaticText("PROF", "Daily " + dailyProfitLossPerc.ToString("+0.00;-0.00;0.00") + "%     ATR " + ATRPips.ToString("0.0") + " Pips     ADX " + ADX.ADX.LastValue.ToString("0") + "     Spread " + spread.ToString("0.0") + " Pips", VerticalAlignment.Top, HorizontalAlignment.Center, mainTextColor);
            //Chart.DrawStaticText("PROF", "Start Bal " + dailyStartingBalance.ToString("0.00") + " Current Bal " + Account.Balance.ToString("0.00") + " Trad Bal " + tradeB.Balance.ToString("0.00")  + " Gross " + tradeB.GrossProfit.ToString("0.00") + " Net " + tradeB.NetProfit.ToString("0.00"), VerticalAlignment.Top, HorizontalAlignment.Center, mainTextColor);
            Chart.DrawStaticText("DIR", "\n" + "EMA" + EMAPeriods.ToString() + "   " + EMAText, VerticalAlignment.Top, HorizontalAlignment.Center, mainTextColor);
            if (ShowBuySell)
                Chart.DrawStaticText("ACT", "\n\n" + actionText, VerticalAlignment.Top, HorizontalAlignment.Center, actionColor);
            actionText = "";
            if (Symbol.Spread > 2.5)
                actionText = "CHECK SPREAD! ";
            if (actionText != "")
                Chart.DrawStaticText("SPREAD", "\n\n\n" + actionText, VerticalAlignment.Top, HorizontalAlignment.Center, Color.OrangeRed);


            var arrowName = string.Format("arrow {0}", index);
            var reversalName = string.Format("reversal {0}", index);

            //if (ShowPatterns)
            //{
            //    // Check Bullish Engulfing
            //    //if (BullishEngulfingCandle(index) && Bars.ClosePrices[index] > TrendMa.Result[index])
            //    if (BullishEngulfingCandle(index))
            //        Chart.DrawIcon(arrowName, ChartIconType.UpArrow, index, (Bars.LowPrices[index] - 0.0002), Color.LimeGreen);

            //    // Check Bearish Engulfing
            //    //if (BearishEngulfingCandle(index) && Bars.ClosePrices[index] < TrendMa.Result[index])
            //    if (BearishEngulfingCandle(index))
            //        Chart.DrawIcon(arrowName, ChartIconType.DownArrow, index, (Bars.HighPrices[index] + 0.0002), Color.Salmon);

            //    // Check reversals
            //    if (EveningStarReversalPattern(index))
            //        // && Bars.ClosePrices[index] > TrendMA.Result[index])
            //        Chart.DrawIcon(reversalName, ChartIconType.Star, index - 1, (Bars.HighPrices[index - 1] + 0.0004), Color.Cyan);
            //    if (MorningStarReversalPattern(index))
            //        // && Bars.ClosePrices[index] < TrendMA.Result[index])
            //        Chart.DrawIcon(reversalName, ChartIconType.Star, index - 1, (Bars.LowPrices[index - 1] - 0.0004), Color.Cyan);
            //}

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
