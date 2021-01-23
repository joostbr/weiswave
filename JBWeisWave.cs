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
		private ATR atr;
		private int waveId = 0;
		private int startWave;	
		private double startPrice;
		private double volume;
		private	Gui.Tools.SimpleFont textFont;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"JBWeisWave";
				Name										= "JBWeisWave";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= true;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
				Length					= 9;
				Style = SwingStyle.Close;
				WaveLineWidth = 2;
				WaveColor = Brushes.DeepSkyBlue;
				DownVolColor = Brushes.Magenta;
				UpVolColor = Brushes.Cyan;
				FontSize = 12;
				LabelBarSpacing = 30;	
				WaveLineStyle = DashStyleHelper.Solid;
				ShowVolumes = true;
				ShowLines = true;
				
			}
			else if (State == State.Configure)
			{
				textFont = new Gui.Tools.SimpleFont("Arial", 12) {Size = FontSize};
			}
			else if (State == State.DataLoaded) {
				this.atr = ATR(Length);
				this.close = new Series<double>(this,MaximumBarsLookBack.TwoHundredFiftySix);
				this.extreme = new Series<double>(this,MaximumBarsLookBack.TwoHundredFiftySix);
				this.direction = new Series<int>(this,MaximumBarsLookBack.TwoHundredFiftySix);
				this.extremeBar = new Series<int>(this,MaximumBarsLookBack.TwoHundredFiftySix);
				this.waveVolume = new Series<double>(this,MaximumBarsLookBack.TwoHundredFiftySix);
			}
		}
		
		private void drawVolumeLabel(String tag, double volume, int barsAgo, double ypos, int yspacing, SolidColorBrush brush) {
			double cryptoCompatibleVol = Instrument.MasterInstrument.InstrumentType == InstrumentType.CryptoCurrency ? Core.Globals.ToCryptocurrencyVolume((long)volume) : volume;
			String text = ""+cryptoCompatibleVol;
			SolidColorBrush textBrush = brush;
			if (text.Length >= 4) {
				text = text.Substring(0, text.Length-3)+"."+text.Substring(1,1)+"k";
			}
			if (tag.Equals("tempVol")) {				
				SolidColorBrush faded =  new SolidColorBrush(Color.FromArgb(170,textBrush.Color.R,textBrush.Color.G,textBrush.Color.B));
				faded.Freeze();
				textBrush = faded;
			}		
			Draw.Text(this, tag, false, text, barsAgo, ypos, yspacing,  textBrush, textFont, TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 0);				
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar == 0) {
				close[0] = (High[0]+Low[0])/2.0;
				extreme[0] = close[0];
				extremeBar[0] = 0;
				direction[0] = 0;
				startWave = 0;
				startPrice = close[0];
				volume=0;
				waveVolume[0]=volume;
				return;
			}
			
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
			extremeBar[0] =  up && high >= prevExtreme ? CurrentBar : down && low <= prevExtreme ? CurrentBar : extremeBar[1];

			double prevVolume = directionHasChanged ? 0 : waveVolume[1];
			if (extremeBar[0]==CurrentBar) {
				double extraVolume = 0;
				for (int i=CurrentBar; i>extremeBar[1]; i--) {
					extraVolume = Volume[CurrentBar-i] + extraVolume;
				}
				waveVolume[0] = prevVolume + extraVolume;				
			}
			else {
				waveVolume[0] = prevVolume;
			}
			
			double ypos;
			
			if (directionHasChanged) {
				
				RemoveDrawObject("temp");
				RemoveDrawObject("tempVol");							
				
				ypos = direction[1]==1 ? High[CurrentBar-extremeBar[1]] : Low[CurrentBar-extremeBar[1]];
				
				if (ShowVolumes) {
					drawVolumeLabel("vol_" + waveId, waveVolume[1], CurrentBar-extremeBar[1], ypos, direction[1] < 0 ? -LabelBarSpacing : LabelBarSpacing, direction[1] < 0 ? DownVolColor : UpVolColor);
				}
				if (ShowLines) {
					Draw.Line(this, "wave_"+waveId , true, CurrentBar - startWave, startPrice, CurrentBar - extremeBar[1], extreme[1], WaveColor, WaveLineStyle, WaveLineWidth);
				}
				
				startPrice=extreme[1];
				startWave = extremeBar[1];
				waveId = waveId + 1;
				
			}						
			else {
				ypos = direction[1]==1 ? High[CurrentBar-extremeBar[0]]  : Low[CurrentBar-extremeBar[0]] ;
				
				if (ShowVolumes) {
					drawVolumeLabel("tempVol", waveVolume[0], CurrentBar-extremeBar[0], ypos, direction[0] < 0 ? -LabelBarSpacing : LabelBarSpacing, direction[0] < 0 ? DownVolColor : UpVolColor);
				}
				if (ShowLines) {				
					Draw.Line(this, "temp", true, CurrentBar - startWave, startPrice, CurrentBar-extremeBar[0] , extreme[0], WaveColor, WaveLineStyle, WaveLineWidth);
				}
			}
			
			
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="ATR Length", Description="Length for ATR", Order=1, GroupName="Parameters")]
		public int Length
		{ get; set; }
		
		[NinjaScriptProperty]
		[Display(Name="Swing Style", Description="Swing style", Order=2, GroupName="Parameters")]
		public SwingStyle Style
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(1, int.MaxValue)]
		[Display(Name="Label-Bar Spacing", Description="Spacing between labels and bars", Order=3, GroupName="Parameters")]
		public int LabelBarSpacing
		{ get; set; }
		
		[NinjaScriptProperty]
		[Range(5, 50)]
		[Display(Name="Volume FontSize", Description="Volume label font size", Order=4, GroupName="Parameters")]
		public int FontSize
		{ get; set; }
		
		[Range(1, int.MaxValue)]
        [NinjaScriptProperty]
        [Display(Name="Line width", Description="Line width for waves", Order = 5, GroupName = "Parameters")]
        public int WaveLineWidth
        {
            get; set;
        }
		
		[NinjaScriptProperty]
        [Display(Name="Line style", Description="Line style for waves", Order = 6, GroupName = "Parameters")]
        public DashStyleHelper WaveLineStyle
        {
            get; set;
        }
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Line Color", Description="Line color for waves", Order=7, GroupName="Parameters")]
		public SolidColorBrush WaveColor
		{ get; set; }

		[Browsable(false)]
		public string WaveColorSerializable
		{
			get { return Serialize.BrushToString(WaveColor); }
			set { WaveColor = (SolidColorBrush)Serialize.StringToBrush(value); }
		}
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Volume Up Color", Description="Label color for volume in up move", Order=8, GroupName="Parameters")]
		public SolidColorBrush UpVolColor
		{ get; set; }

		[Browsable(false)]
		public string UpColorSerializable
		{
			get { return Serialize.BrushToString(UpVolColor); }
			set { UpVolColor = (SolidColorBrush)Serialize.StringToBrush(value); }
		}
		
		[NinjaScriptProperty]
		[XmlIgnore]
		[Display(Name="Volume Down Color", Description="Label color for volume in down move", Order=9, GroupName="Parameters")]
		public SolidColorBrush DownVolColor
		{ get; set; }

		[Browsable(false)]
		public string DownColorSerializable
		{
			get { return Serialize.BrushToString(DownVolColor); }
			set { DownVolColor = (SolidColorBrush)Serialize.StringToBrush(value); }
		}
		
		[NinjaScriptProperty]
        [Display(Name="Show Waves", Description="Show Waves", Order = 10, GroupName = "Parameters")]
        public bool ShowLines
        {
            get; set;
        }

		[NinjaScriptProperty]
        [Display(Name="Show Volumes", Description="Show Volumes", Order = 11, GroupName = "Parameters")]
        public bool ShowVolumes
        {
            get; set;
        }

		#endregion

	}
}

namespace JBWeisWave.Base {
	
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
		public JBWeisWave JBWeisWave(int length, SwingStyle style, int labelBarSpacing, int fontSize, int waveLineWidth, DashStyleHelper waveLineStyle, SolidColorBrush waveColor, SolidColorBrush upVolColor, SolidColorBrush downVolColor, bool showLines, bool showVolumes)
		{
			return JBWeisWave(Input, length, style, labelBarSpacing, fontSize, waveLineWidth, waveLineStyle, waveColor, upVolColor, downVolColor, showLines, showVolumes);
		}

		public JBWeisWave JBWeisWave(ISeries<double> input, int length, SwingStyle style, int labelBarSpacing, int fontSize, int waveLineWidth, DashStyleHelper waveLineStyle, SolidColorBrush waveColor, SolidColorBrush upVolColor, SolidColorBrush downVolColor, bool showLines, bool showVolumes)
		{
			if (cacheJBWeisWave != null)
				for (int idx = 0; idx < cacheJBWeisWave.Length; idx++)
					if (cacheJBWeisWave[idx] != null && cacheJBWeisWave[idx].Length == length && cacheJBWeisWave[idx].Style == style && cacheJBWeisWave[idx].LabelBarSpacing == labelBarSpacing && cacheJBWeisWave[idx].FontSize == fontSize && cacheJBWeisWave[idx].WaveLineWidth == waveLineWidth && cacheJBWeisWave[idx].WaveLineStyle == waveLineStyle && cacheJBWeisWave[idx].WaveColor == waveColor && cacheJBWeisWave[idx].UpVolColor == upVolColor && cacheJBWeisWave[idx].DownVolColor == downVolColor && cacheJBWeisWave[idx].ShowLines == showLines && cacheJBWeisWave[idx].ShowVolumes == showVolumes && cacheJBWeisWave[idx].EqualsInput(input))
						return cacheJBWeisWave[idx];
			return CacheIndicator<JBWeisWave>(new JBWeisWave(){ Length = length, Style = style, LabelBarSpacing = labelBarSpacing, FontSize = fontSize, WaveLineWidth = waveLineWidth, WaveLineStyle = waveLineStyle, WaveColor = waveColor, UpVolColor = upVolColor, DownVolColor = downVolColor, ShowLines = showLines, ShowVolumes = showVolumes }, input, ref cacheJBWeisWave);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.JBWeisWave JBWeisWave(int length, SwingStyle style, int labelBarSpacing, int fontSize, int waveLineWidth, DashStyleHelper waveLineStyle, SolidColorBrush waveColor, SolidColorBrush upVolColor, SolidColorBrush downVolColor, bool showLines, bool showVolumes)
		{
			return indicator.JBWeisWave(Input, length, style, labelBarSpacing, fontSize, waveLineWidth, waveLineStyle, waveColor, upVolColor, downVolColor, showLines, showVolumes);
		}

		public Indicators.JBWeisWave JBWeisWave(ISeries<double> input , int length, SwingStyle style, int labelBarSpacing, int fontSize, int waveLineWidth, DashStyleHelper waveLineStyle, SolidColorBrush waveColor, SolidColorBrush upVolColor, SolidColorBrush downVolColor, bool showLines, bool showVolumes)
		{
			return indicator.JBWeisWave(input, length, style, labelBarSpacing, fontSize, waveLineWidth, waveLineStyle, waveColor, upVolColor, downVolColor, showLines, showVolumes);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.JBWeisWave JBWeisWave(int length, SwingStyle style, int labelBarSpacing, int fontSize, int waveLineWidth, DashStyleHelper waveLineStyle, SolidColorBrush waveColor, SolidColorBrush upVolColor, SolidColorBrush downVolColor, bool showLines, bool showVolumes)
		{
			return indicator.JBWeisWave(Input, length, style, labelBarSpacing, fontSize, waveLineWidth, waveLineStyle, waveColor, upVolColor, downVolColor, showLines, showVolumes);
		}

		public Indicators.JBWeisWave JBWeisWave(ISeries<double> input , int length, SwingStyle style, int labelBarSpacing, int fontSize, int waveLineWidth, DashStyleHelper waveLineStyle, SolidColorBrush waveColor, SolidColorBrush upVolColor, SolidColorBrush downVolColor, bool showLines, bool showVolumes)
		{
			return indicator.JBWeisWave(input, length, style, labelBarSpacing, fontSize, waveLineWidth, waveLineStyle, waveColor, upVolColor, downVolColor, showLines, showVolumes);
		}
	}
}

#endregion
