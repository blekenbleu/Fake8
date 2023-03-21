using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.IO;
using System.IO.Ports;
using System.Timers;

/* Tension test sliders
 ; https://github.com/blekenbleu/SimHub-profiles/blob/main/Fake8.shsds
 ; f0:	PWM period (usec / 50) 1 - 40000
 ; f1:  max PWM % 0 - 100
 ; f2:  min PWM % 0 - 10
 ; f3:  predistortion amplitude: % of PWM change 0 - 64
 ; f4:  predistortion count (number of PWM cycles) 0 - 64
 ; f5:  Test period count (number of SimHub Run() invocations 60 - 600
 ; f6:  Test rise count 0 - 64
 ; f7:  Test hold count 0 - 64
 ; f8:  Test fall count 0 - 64
 ; f9:
 */

namespace Fake8plugin
{
	public class Fake8		// handle real Arduino USB COM por with 8-bit data
	{
		private static SerialPort Arduino;
		static bool ongoing;						// Arduino port statu flag
		static internal string Ini = "Fake7.";		// SimHub's property prefix for this plugin
		private string[] Prop, b4;					// kill duplicated Custom Serial messages
		private byte[] cmd;							// 8-bit bytes to Arduino
		static internal string msg;					// user feedback property

		/// <summary>
		/// wraps SimHub.Logging.Current.Info() with prefix
		/// </summary>
		private static bool Info(string str)
		{
			SimHub.Logging.Current.Info("Fake8." + str);								// bool Info()
			return true;
		}

		static internal bool Recover(SerialPort port)
		{
			if (port.IsOpen)
				return true;
			else
			{
				try
				{
					port.DiscardInBuffer();
				}
				catch {/* ignore */}
				try
				{
					port.DiscardOutBuffer();
				}
				catch {/* ignore */}
				try
				{
					port.Open();
					return true;
				}
				catch { /* ignore */ }
			}

			return false;
		}

		private void TryWrite(byte[] cmd, byte length)
		{
			try
			{
				Arduino.Write(cmd, 0, length);
			}
			catch (Exception wex)
			{
				if (ongoing)
					Info(msg = "Run():  " + wex.Message + " during Arduino.Write");
				if (ongoing = Recover(Arduino))
					Info(msg = "Run():  Arduino connection restored");
			} 
		}

		/// <summary>
		// Called one time per game data update, contains all normalized game data,
		/// Update run time
		/// </summary>
		bool once;											// avoid flooding log with duplicate messages
		internal void Run(PluginManager pluginManager)
		{
			if (null == Prop)
			{
				if (once)
					Info(msg = "Run(): null Prop[]");
				once = false;
				return;
			}
			once = true;
			Recover(Fake7.CustomSerial);
				
			for (byte i = 0; i < Prop.Length; i++)
			{
				string prop = pluginManager.GetPropertyValue(Ini + Prop[i])?.ToString();

				if (null == prop || 0 == prop.Length || (prop.Length == b4[i].Length && prop == b4[i]))
					continue;
				uint value = uint.Parse(prop);

				b4[i] = prop;
//							https://github.com/blekenbleu/Arduino-Blue-Pill/tree/main/PWM_FullConfiguration
				if (0 == i)			// case 7: 16-bit PWM period
				{
					cmd[2] = (byte)(127 & value);
					value >>= 7;
					cmd[1] = (byte)(127 & value);
					value >>= 7;
					cmd[0] = (byte)((7 << 5) | (3 & value));
					TryWrite(cmd, 3);
				}
				else if (1 == i) 	// case 4: 7-bit PWM %
				{
					cmd[1] = (byte)(127 & value);
					cmd[0] = (byte)(4 << 5);
					TryWrite(cmd, 2);
				}
			}
		}

		/// <summary>
		/// declare a delegate for Receiver()
		/// </summary>
		private delegate void CustDel(string text);
		readonly CustDel Crcv = Receiver;

		/// <summary>
		/// Called by delegate from DataReceived method AndroidDataReceived(),
		/// which runs on a secondary thread from ThreadPool, so should not directly access non-static main thread variables
		/// As a delegate, it must be static 
		/// </summary>
		static string old;
		static char last;
		static private void Receiver(string received)
		{
			if (String.Empty == received || (old.Length == received.Length && old == received))
				return;

			try
			{
				Fake7.CustomSerial.Write(old = received);
			}
			catch (Exception e)
			{
				if (Fake7.running)
					Info(Fake7.old = "Custom Serial:  " + e.Message + $" during Fake7.CustomSerial.Write({received})");
				if (Fake7.running = Recover(Fake7.CustomSerial))
					Info(Fake7.old = "Custom Serial connection recovered");
			}
			if ('\n' == last)	// Arduino messages may be fragmented, but end with `\n'
				msg = old;
			else msg += old;
			last = old[old.Length - 1];
		}

		/// <summary>
		/// Arduino DataReceived method runs on a secondary thread from ThreadPool
		/// calls Receiver() via delegate
		/// </summary>
		private void AndroidDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			SerialPort sp = (SerialPort)sender;
			while (ongoing)
			{
				try
				{
					string s = sp.ReadExisting();

					Crcv(s);	// pass current instance to Receiver() delegate
				}
				catch (Exception rex)
				{
					if (ongoing)
						Info(msg = "AndroidDataReceived():  " + rex.Message );
					ongoing = false;		// recover in TryWrite();
				}
			}
		}

		/// <summary>
		/// Called at plugin manager stop, close/dispose anything needed here!
		/// Plugins are rebuilt at game changes, but NCalc files are not re-read
		/// </summary>
		public void End(Fake7 F7)
		{
			ongoing = false;
			F7.Close(Arduino);
		}

		/// <summary>
		/// Called at SimHub start then after game changes
		/// </summary>
		public void Init(PluginManager pluginManager, Fake7 F7)
		{
			old = "old";
			msg = "[waiting]";
			last = '\n';
			once = true;
			Arduino = new SerialPort();
			cmd = new byte[4];

// read properties and configure

			string parms = pluginManager.GetPropertyValue(Fake7.Ini + "parms")?.ToString();

			if (null != parms && 0 < parms.Length)
			{
				Prop = parms.Split(',');
				if (5 < Prop.Length)
				{
					b4 = new string[Prop.Length];
					for (byte i = 0; i < Prop.Length; i++)
						b4[i] = "";
				}
				else Info($"Init():  {Fake7.Ini + "parms"}.Length {Prop.Length} < expected 6");
			}
			else Info("Init():  missing " + Fake7.Ini + "parms");

			string pill = pluginManager.GetPropertyValue(Fake7.Ini + "pill")?.ToString();
			if (null != pill || 0 < pill.Length)
			{													// launch serial port

				ongoing = true;
				Arduino.DataReceived += AndroidDataReceived;
				F7.Fopen(Arduino, pill);
			}
			else F7.Sports(Fake7.Ini + "Custom Serial 'F8pill' missing from F8.ini");
		}																			// Init()
	}
}
