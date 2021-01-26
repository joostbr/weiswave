#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using JBWeisWave.Base;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{


    public class JBWeisWave : Indicator
    {

        private Series<double> close;
        private Series<int> direction;
        private Series<double> extreme;
        private Series<int> extremeBar;
        private Series<double> waveVolume;
        private Series<double> waveVolumeDelta;
        private Series<double> barVolumeDelta;
        private ATR atr;
        private int waveId = 0;
        private int startWave;
        private double startPrice;
        private double volume;
        private double buys;
        private double sells;
        private double tailVolume;
        private double tailDelta;
        private bool validConfig = true;


        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"JBWeisWave";
                Name = "JBWeisWave";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                DrawHorizontalGridLines = true;
                DrawVerticalGridLines = true;
                PaintPriceMarkers = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                //Disable this property if your indicator requires custom values that cumulate with each new market data event. 
                //See Help Guide for additional information.
                IsSuspendedWhileInactive = true;
                Length = 9;
                Style = SwingStyle.Close;
                WaveLineWidth = 2;
                WaveColor = Brushes.DeepSkyBlue;
                DownVolColor = Brushes.White;
                UpVolColor = Brushes.White;
                DownDeltaColor = Brushes.Magenta;
                UpDeltaColor = Brushes.Cyan;
                VolumeLabelBarSpacing = 30;
                DeltaLabelBarSpacing = 50;
                WaveLineStyle = DashStyleHelper.Solid;
                ShowVolumes = true;
                ShowLines = true;
                ShowDeltas = true;
                ShowTail = false;

                VolumeTextFont = new Gui.Tools.SimpleFont("Arial", 11);
                DeltaTextFont = new Gui.Tools.SimpleFont("Arial", 11);

            }
            else if (State == State.Configure)
            {
                if (Calculate != Calculate.OnBarClose)
                {
                    Draw.TextFixed(this, "NinjaScriptInfo", "JBWeisWave only works with calculation set to 'on bar close'", TextPosition.BottomRight);
                    Log("JBWeisWave only works with calculation set to 'on bar close'", LogLevel.Error);
                    validConfig = false;
                }
            }
            else if (State == State.Historical)
            {
                if (ShowDeltas && !Bars.IsTickReplay)
                {
                    Draw.TextFixed(this, "NinjaScriptInfo", "JBWeisWave needs tick replay enabled on the data series when using delta", TextPosition.BottomRight);
                    Log("JBWeisWave needs tick replay enabled on the data series when using delta", LogLevel.Error);
                }
            }
            else if (State == State.DataLoaded)
            {
                this.atr = ATR(Length);
                this.close = new Series<double>(this, MaximumBarsLookBack.TwoHundredFiftySix);
                this.extreme = new Series<double>(this, MaximumBarsLookBack.TwoHundredFiftySix);
                this.direction = new Series<int>(this, MaximumBarsLookBack.TwoHundredFiftySix);
                this.extremeBar = new Series<int>(this, MaximumBarsLookBack.TwoHundredFiftySix);
                this.waveVolume = new Series<double>(this, MaximumBarsLookBack.TwoHundredFiftySix);
                this.waveVolumeDelta = new Series<double>(this, MaximumBarsLookBack.TwoHundredFiftySix);
                this.barVolumeDelta = new Series<double>(this, MaximumBarsLookBack.TwoHundredFiftySix);
            }
        }

        protected override void OnMarketData(MarketDataEventArgs e)
        {
            if (e.MarketDataType == MarketDataType.Last)
            {
                if (e.Price >= e.Ask)
                    buys += (Instrument.MasterInstrument.InstrumentType == Cbi.InstrumentType.CryptoCurrency ? Core.Globals.ToCryptocurrencyVolume(e.Volume) : e.Volume);
                else if (e.Price <= e.Bid)
                    sells += (Instrument.MasterInstrument.InstrumentType == Cbi.InstrumentType.CryptoCurrency ? Core.Globals.ToCryptocurrencyVolume(e.Volume) : e.Volume);
            }
        }

        private void drawVolumeLabel(String tag, double volume, int barsAgo, double ypos, int yspacing, SolidColorBrush brush)
        {
            double cryptoCompatibleVol = Instrument.MasterInstrument.InstrumentType == InstrumentType.CryptoCurrency ? Core.Globals.ToCryptocurrencyVolume((long)volume) : volume;
            String text = "" + cryptoCompatibleVol;
            SolidColorBrush textBrush = brush;
            if (volume >= 0 && text.Length >= 4)
            {
                text = text.Substring(0, text.Length - 3) + "." + text.Substring(1, 1) + "k";
            }
            else if (volume < 0 && text.Length >= 5)
            {
                text = text.Substring(0, text.Length - 3) + "." + text.Substring(2, 1) + "k";
            }
            if (tag.StartsWith("temp"))
            {
                SolidColorBrush faded = new SolidColorBrush(Color.FromArgb(170, textBrush.Color.R, textBrush.Color.G, textBrush.Color.B));
                faded.Freeze();
                textBrush = faded;
            }
            Draw.Text(this, tag, false, text, barsAgo, ypos, yspacing, textBrush, VolumeTextFont, TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
        }

        private void drawVolumeDeltaLabel(String tag, double delta, int barsAgo, double ypos, int yspacing, SolidColorBrush brush)
        {
            double cryptoCompatibleVol = Instrument.MasterInstrument.InstrumentType == InstrumentType.CryptoCurrency ? Core.Globals.ToCryptocurrencyVolume((long)delta) : delta;
            String text = "" + cryptoCompatibleVol;
            SolidColorBrush textBrush = brush;
            if (delta >= 0 && text.Length >= 4)
            {
                text = text.Substring(0, text.Length - 3) + "." + text.Substring(1, 1) + "k";
            }
            else if (delta < 0 && text.Length >= 5)
            {
                text = text.Substring(0, text.Length - 3) + "." + text.Substring(2, 1) + "k";
            }
            if (tag.StartsWith("temp"))
            {
                SolidColorBrush faded = new SolidColorBrush(Color.FromArgb(170, textBrush.Color.R, textBrush.Color.G, textBrush.Color.B));
                faded.Freeze();
                textBrush = faded;
            }
            text = "\u0394 " + text;
            Draw.Text(this, tag, false, text, barsAgo, ypos, yspacing, textBrush, DeltaTextFont, TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);
        }

        protected override void OnBarUpdate()
        {
            if (!validConfig)
            {
                return;
            }

            if (CurrentBar == 0)
            {
                close[0] = (High[0] + Low[0]) / 2.0;
                extreme[0] = close[0];
                extremeBar[0] = 0;
                direction[0] = 0;
                startWave = 0;
                startPrice = close[0];
                waveVolume[0] = 0;
                waveVolumeDelta[0] = 0;
                barVolumeDelta[0] = 0;
                return;
            }

            barVolumeDelta[0] = buys - sells;
            buys = 0;
            sells = 0;

            //Print(Time[0]+" "+barVolumeDelta[0]+"   "+Volume[0]);

            double high = Style == SwingStyle.HighLow ? High[0] : Close[0];
            double low = Style == SwingStyle.HighLow ? Low[0] : Close[0];

            double previousClose = close[1];

            double previousHigh = previousClose + this.atr[0];
            double previousLow = previousClose - this.atr[0];

            close[0] = high > previousHigh ? high : low < previousLow ? low : previousClose;
            direction[0] = close[0] > previousClose ? 1 : close[0] < previousClose ? -1 : direction[1];

            bool directionHasChanged = direction[1] != direction[0];
            bool up = direction[0] > 0;
            bool down = direction[0] < 0;

            double prevExtreme = directionHasChanged ? close[0] : extreme[1];
            extreme[0] = up && high >= prevExtreme ? high : down && low <= prevExtreme ? low : prevExtreme;
            extremeBar[0] = up && high >= prevExtreme ? CurrentBar : down && low <= prevExtreme ? CurrentBar : extremeBar[1];

            double prevVolume = directionHasChanged ? 0 : waveVolume[1];
            double prevDelta = directionHasChanged ? 0 : waveVolumeDelta[1];
            if (extremeBar[0] == CurrentBar)
            {
                double extraVolume = 0;
                double extraDelta = 0;
                for (int i = CurrentBar; i > extremeBar[1]; i--)
                {
                    extraVolume = Volume[CurrentBar - i] + extraVolume;
                    extraDelta = barVolumeDelta[CurrentBar - i] + extraDelta;
                }
                waveVolume[0] = prevVolume + extraVolume;
                waveVolumeDelta[0] = prevDelta + extraDelta;

                tailVolume = 0;
                tailDelta = 0;
            }
            else
            {
                waveVolume[0] = prevVolume;
                waveVolumeDelta[0] = prevDelta;

                tailVolume = tailVolume + Volume[0];
                tailDelta = tailDelta + barVolumeDelta[0];
            }

            double ypos;

            if (directionHasChanged)
            {

                RemoveDrawObject("temp");
                RemoveDrawObject("tempVol");
                RemoveDrawObject("tempDelta");
                RemoveDrawObject("tempTail");
                RemoveDrawObject("tempDeltaTail");
                RemoveDrawObject("tempVolTail");

                ypos = direction[1] == 1 ? High[CurrentBar - extremeBar[1]] : Low[CurrentBar - extremeBar[1]];

                if (ShowVolumes)
                {
                    drawVolumeLabel("vol_" + waveId, waveVolume[1], CurrentBar - extremeBar[1], ypos, direction[1] < 0 ? -VolumeLabelBarSpacing : VolumeLabelBarSpacing, direction[1] < 0 ? DownVolColor : UpVolColor);
                }
                if (ShowDeltas)
                {
                    drawVolumeDeltaLabel("delta_" + waveId, waveVolumeDelta[1], CurrentBar - extremeBar[1], ypos, direction[1] < 0 ? -DeltaLabelBarSpacing : DeltaLabelBarSpacing, waveVolumeDelta[1] < 0 ? DownDeltaColor : UpDeltaColor);
                }
                if (ShowLines)
                {
                    Draw.Line(this, "wave_" + waveId, true, CurrentBar - startWave, startPrice, CurrentBar - extremeBar[1], extreme[1], WaveColor, WaveLineStyle, WaveLineWidth);
                }

                startPrice = extreme[1];
                startWave = extremeBar[1];
                waveId = waveId + 1;

            }


            double yposTail = direction[1] == 1 ? Low[0] : High[0];
            ypos = direction[1] == 1 ? High[CurrentBar - extremeBar[0]] : Low[CurrentBar - extremeBar[0]];

            if (ShowVolumes)
            {
                drawVolumeLabel("tempVol", waveVolume[0], CurrentBar - extremeBar[0], ypos, direction[0] < 0 ? -VolumeLabelBarSpacing : VolumeLabelBarSpacing, direction[0] < 0 ? DownVolColor : UpVolColor);
                if (ShowTail)
                {
                    RemoveDrawObject("tempVolTail");
                    if (CurrentBar > extremeBar[0])
                    {
                        drawVolumeLabel("tempVolTail", tailVolume, 0, yposTail, direction[0] > 0 ? -VolumeLabelBarSpacing : VolumeLabelBarSpacing, tailVolume < 0 ? DownVolColor : UpVolColor);
                    }
                }
            }
            if (ShowDeltas)
            {
                drawVolumeDeltaLabel("tempDelta", waveVolumeDelta[0], CurrentBar - extremeBar[0], ypos, direction[0] < 0 ? -DeltaLabelBarSpacing : DeltaLabelBarSpacing, waveVolumeDelta[0] < 0 ? DownDeltaColor : UpDeltaColor);
                if (ShowTail)
                {
                    RemoveDrawObject("tempDeltaTail");
                    if (CurrentBar > extremeBar[0])
                    {
                        drawVolumeDeltaLabel("tempDeltaTail", tailDelta, 0, yposTail, direction[0] > 0 ? -DeltaLabelBarSpacing : DeltaLabelBarSpacing, tailDelta < 0 ? DownDeltaColor : UpDeltaColor);
                    }
                }
            }
            if (ShowLines)
            {
                Draw.Line(this, "temp", true, CurrentBar - startWave, startPrice, CurrentBar - extremeBar[0], extreme[0], WaveColor, WaveLineStyle, WaveLineWidth);
                if (ShowTail)
                {
                    RemoveDrawObject("tempTail");
                    if (CurrentBar > extremeBar[0])
                    {
                        double tailEndPosY = Style == SwingStyle.HighLow ? (direction[1] > 0 ? Low[0] : High[0]) : Close[0];
                        Draw.Line(this, "tempTail", true, CurrentBar - extremeBar[0], extreme[0], 0, tailEndPosY, WaveColor, WaveLineStyle, WaveLineWidth);
                    }
                }
            }



        }

        #region Properties
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ATR Length", Description = "Length for ATR", Order = 1, GroupName = "1. Wave Settings")]
        public int Length
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Swing Style", Description = "Swing style", Order = 2, GroupName = "1. Wave Settings")]
        public SwingStyle Style
        { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Waves", Description = "Show Waves", Order = 3, GroupName = "1. Wave Settings")]
        public bool ShowLines
        {
            get; set;
        }

        [NinjaScriptProperty]
        [Display(Name = "Show Volumes", Description = "Show Volumes", Order = 4, GroupName = "1. Wave Settings")]
        public bool ShowVolumes
        {
            get; set;
        }

        [NinjaScriptProperty]
        [Display(Name = "Show Volume Deltas", Description = "Show Volume Deltas", Order = 5, GroupName = "1. Wave Settings")]
        public bool ShowDeltas
        {
            get; set;
        }

        [NinjaScriptProperty]
        [Display(Name = "Show Tail (Beta)", Description = "Show wave tail", Order = 6, GroupName = "1. Wave Settings")]
        public bool ShowTail
        {
            get; set;
        }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Volume Label-Bar Spacing", Description = "Spacing between volume labels and bars", Order = 1, GroupName = "2. Visuals")]
        public int VolumeLabelBarSpacing
        { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Delta Label-Bar Spacing", Description = "Spacing between delta labels and bars", Order = 2, GroupName = "2. Visuals")]
        public int DeltaLabelBarSpacing
        { get; set; }

        [Range(1, int.MaxValue)]
        [NinjaScriptProperty]
        [Display(Name = "Line width", Description = "Line width for waves", Order = 3, GroupName = "2. Visuals")]
        public int WaveLineWidth
        {
            get; set;
        }

        [NinjaScriptProperty]
        [Display(Name = "Line style", Description = "Line style for waves", Order = 4, GroupName = "2. Visuals")]
        public DashStyleHelper WaveLineStyle
        {
            get; set;
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Line Color", Description = "Line color for waves", Order = 5, GroupName = "2. Visuals")]
        public SolidColorBrush WaveColor
        { get; set; }

        [Browsable(false)]
        public string WaveColorSerializable
        {
            get { return Serialize.BrushToString(WaveColor); }
            set { WaveColor = (SolidColorBrush)Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Volume Text Font", Description = "Represents the text font for the volume values", Order = 6, GroupName = "2. Visuals")]
        public NinjaTrader.Gui.Tools.SimpleFont VolumeTextFont
        {
            get; set;
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Volume Up Color", Description = "Label color for volume in up move", Order = 7, GroupName = "2. Visuals")]
        public SolidColorBrush UpVolColor
        { get; set; }

        [Browsable(false)]
        public string UpVolColorSerializable
        {
            get { return Serialize.BrushToString(UpVolColor); }
            set { UpVolColor = (SolidColorBrush)Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Volume Down Color", Description = "Label color for volume in down move", Order = 8, GroupName = "2. Visuals")]
        public SolidColorBrush DownVolColor
        { get; set; }

        [Browsable(false)]
        public string DownVolColorSerializable
        {
            get { return Serialize.BrushToString(DownVolColor); }
            set { DownVolColor = (SolidColorBrush)Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Delta Text Font", Description = "Represents the text font for the delta values", Order = 9, GroupName = "2. Visuals")]
        public NinjaTrader.Gui.Tools.SimpleFont DeltaTextFont
        {
            get; set;
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Volume Delta Color", Description = "Label color for delta in up move", Order = 10, GroupName = "2. Visuals")]
        public SolidColorBrush UpDeltaColor
        { get; set; }

        [Browsable(false)]
        public string UpDeltaColorSerializable
        {
            get { return Serialize.BrushToString(UpDeltaColor); }
            set { UpDeltaColor = (SolidColorBrush)Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Volume Down Color", Description = "Label color for delta in down move", Order = 11, GroupName = "2. Visuals")]
        public SolidColorBrush DownDeltaColor
        { get; set; }

        [Browsable(false)]
        public string DownDeltaColorSerializable
        {
            get { return Serialize.BrushToString(DownDeltaColor); }
            set { DownDeltaColor = (SolidColorBrush)Serialize.StringToBrush(value); }
        }



        #endregion

    }
}

namespace JBWeisWave.Base
{

    public enum SwingStyle
    {
        HighLow,
        Close
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
    {
        private JBWeisWave[] cacheJBWeisWave;
        public JBWeisWave JBWeisWave(int length, SwingStyle style, bool showLines, bool showVolumes, bool showDeltas, bool showTail, int volumeLabelBarSpacing, int deltaLabelBarSpacing, int waveLineWidth, DashStyleHelper waveLineStyle, SolidColorBrush waveColor, NinjaTrader.Gui.Tools.SimpleFont volumeTextFont, SolidColorBrush upVolColor, SolidColorBrush downVolColor, NinjaTrader.Gui.Tools.SimpleFont deltaTextFont, SolidColorBrush upDeltaColor, SolidColorBrush downDeltaColor)
        {
            return JBWeisWave(Input, length, style, showLines, showVolumes, showDeltas, showTail, volumeLabelBarSpacing, deltaLabelBarSpacing, waveLineWidth, waveLineStyle, waveColor, volumeTextFont, upVolColor, downVolColor, deltaTextFont, upDeltaColor, downDeltaColor);
        }

        public JBWeisWave JBWeisWave(ISeries<double> input, int length, SwingStyle style, bool showLines, bool showVolumes, bool showDeltas, bool showTail, int volumeLabelBarSpacing, int deltaLabelBarSpacing, int waveLineWidth, DashStyleHelper waveLineStyle, SolidColorBrush waveColor, NinjaTrader.Gui.Tools.SimpleFont volumeTextFont, SolidColorBrush upVolColor, SolidColorBrush downVolColor, NinjaTrader.Gui.Tools.SimpleFont deltaTextFont, SolidColorBrush upDeltaColor, SolidColorBrush downDeltaColor)
        {
            if (cacheJBWeisWave != null)
                for (int idx = 0; idx < cacheJBWeisWave.Length; idx++)
                    if (cacheJBWeisWave[idx] != null && cacheJBWeisWave[idx].Length == length && cacheJBWeisWave[idx].Style == style && cacheJBWeisWave[idx].ShowLines == showLines && cacheJBWeisWave[idx].ShowVolumes == showVolumes && cacheJBWeisWave[idx].ShowDeltas == showDeltas && cacheJBWeisWave[idx].ShowTail == showTail && cacheJBWeisWave[idx].VolumeLabelBarSpacing == volumeLabelBarSpacing && cacheJBWeisWave[idx].DeltaLabelBarSpacing == deltaLabelBarSpacing && cacheJBWeisWave[idx].WaveLineWidth == waveLineWidth && cacheJBWeisWave[idx].WaveLineStyle == waveLineStyle && cacheJBWeisWave[idx].WaveColor == waveColor && cacheJBWeisWave[idx].VolumeTextFont == volumeTextFont && cacheJBWeisWave[idx].UpVolColor == upVolColor && cacheJBWeisWave[idx].DownVolColor == downVolColor && cacheJBWeisWave[idx].DeltaTextFont == deltaTextFont && cacheJBWeisWave[idx].UpDeltaColor == upDeltaColor && cacheJBWeisWave[idx].DownDeltaColor == downDeltaColor && cacheJBWeisWave[idx].EqualsInput(input))
                        return cacheJBWeisWave[idx];
            return CacheIndicator<JBWeisWave>(new JBWeisWave() { Length = length, Style = style, ShowLines = showLines, ShowVolumes = showVolumes, ShowDeltas = showDeltas, ShowTail = showTail, VolumeLabelBarSpacing = volumeLabelBarSpacing, DeltaLabelBarSpacing = deltaLabelBarSpacing, WaveLineWidth = waveLineWidth, WaveLineStyle = waveLineStyle, WaveColor = waveColor, VolumeTextFont = volumeTextFont, UpVolColor = upVolColor, DownVolColor = downVolColor, DeltaTextFont = deltaTextFont, UpDeltaColor = upDeltaColor, DownDeltaColor = downDeltaColor }, input, ref cacheJBWeisWave);
        }
    }
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
    public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
    {
        public Indicators.JBWeisWave JBWeisWave(int length, SwingStyle style, bool showLines, bool showVolumes, bool showDeltas, bool showTail, int volumeLabelBarSpacing, int deltaLabelBarSpacing, int waveLineWidth, DashStyleHelper waveLineStyle, SolidColorBrush waveColor, NinjaTrader.Gui.Tools.SimpleFont volumeTextFont, SolidColorBrush upVolColor, SolidColorBrush downVolColor, NinjaTrader.Gui.Tools.SimpleFont deltaTextFont, SolidColorBrush upDeltaColor, SolidColorBrush downDeltaColor)
        {
            return indicator.JBWeisWave(Input, length, style, showLines, showVolumes, showDeltas, showTail, volumeLabelBarSpacing, deltaLabelBarSpacing, waveLineWidth, waveLineStyle, waveColor, volumeTextFont, upVolColor, downVolColor, deltaTextFont, upDeltaColor, downDeltaColor);
        }

        public Indicators.JBWeisWave JBWeisWave(ISeries<double> input, int length, SwingStyle style, bool showLines, bool showVolumes, bool showDeltas, bool showTail, int volumeLabelBarSpacing, int deltaLabelBarSpacing, int waveLineWidth, DashStyleHelper waveLineStyle, SolidColorBrush waveColor, NinjaTrader.Gui.Tools.SimpleFont volumeTextFont, SolidColorBrush upVolColor, SolidColorBrush downVolColor, NinjaTrader.Gui.Tools.SimpleFont deltaTextFont, SolidColorBrush upDeltaColor, SolidColorBrush downDeltaColor)
        {
            return indicator.JBWeisWave(input, length, style, showLines, showVolumes, showDeltas, showTail, volumeLabelBarSpacing, deltaLabelBarSpacing, waveLineWidth, waveLineStyle, waveColor, volumeTextFont, upVolColor, downVolColor, deltaTextFont, upDeltaColor, downDeltaColor);
        }
    }
}

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
    {
        public Indicators.JBWeisWave JBWeisWave(int length, SwingStyle style, bool showLines, bool showVolumes, bool showDeltas, bool showTail, int volumeLabelBarSpacing, int deltaLabelBarSpacing, int waveLineWidth, DashStyleHelper waveLineStyle, SolidColorBrush waveColor, NinjaTrader.Gui.Tools.SimpleFont volumeTextFont, SolidColorBrush upVolColor, SolidColorBrush downVolColor, NinjaTrader.Gui.Tools.SimpleFont deltaTextFont, SolidColorBrush upDeltaColor, SolidColorBrush downDeltaColor)
        {
            return indicator.JBWeisWave(Input, length, style, showLines, showVolumes, showDeltas, showTail, volumeLabelBarSpacing, deltaLabelBarSpacing, waveLineWidth, waveLineStyle, waveColor, volumeTextFont, upVolColor, downVolColor, deltaTextFont, upDeltaColor, downDeltaColor);
        }

        public Indicators.JBWeisWave JBWeisWave(ISeries<double> input, int length, SwingStyle style, bool showLines, bool showVolumes, bool showDeltas, bool showTail, int volumeLabelBarSpacing, int deltaLabelBarSpacing, int waveLineWidth, DashStyleHelper waveLineStyle, SolidColorBrush waveColor, NinjaTrader.Gui.Tools.SimpleFont volumeTextFont, SolidColorBrush upVolColor, SolidColorBrush downVolColor, NinjaTrader.Gui.Tools.SimpleFont deltaTextFont, SolidColorBrush upDeltaColor, SolidColorBrush downDeltaColor)
        {
            return indicator.JBWeisWave(input, length, style, showLines, showVolumes, showDeltas, showTail, volumeLabelBarSpacing, deltaLabelBarSpacing, waveLineWidth, waveLineStyle, waveColor, volumeTextFont, upVolColor, downVolColor, deltaTextFont, upDeltaColor, downDeltaColor);
        }
    }
}

#endregion
