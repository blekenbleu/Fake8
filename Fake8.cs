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
		static bool ongoing;
		private static SerialPort Arduino;
		static internal string Ini = "Fake7.";
		private string[] Prop, b4;
		private byte[] cmd;
		internal string Label;

		/// <summary>
		/// wraps SimHub.Logging.Current.Info() with prefix
		/// </summary>
		private static bool Info(string str)
		{
			SimHub.Logging.Current.Info("Fake8." + str);								// bool Info()
			return true;
		}

		/// <summary>
		// Called one time per game data update, contains all normalized game data,
		/// Update run time
		/// </summary>
		internal string Run(PluginManager pluginManager)
		{
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
					Arduino.Write(cmd, 0, 3);
				}
				else if (1 == i) 	// case 4: 7-bit PWM %
				{
					cmd[1] = (byte)(127 & value);
					cmd[0] = (byte)(4 << 5);
					Arduino.Write(cmd, 0, 2);
				}
			}
			return Ini + Label;
		}

		/// <summary>
		/// declare a delegate for Fake8receiver()
		/// </summary>
		private delegate void CustDel(string text);
		readonly CustDel Crcv = Fake8receiver;

		/// <summary>
		/// Called by delegate from DataReceived method AndroidDataReceived(),
		/// which runs on a secondary thread from ThreadPool, so should not directly access non-static main thread variables
		/// As a delegate, it must be static 
		/// </summary>
		static string old, rcv;
		static private void Fake8receiver(string received)
		{
			try
			{
				if (String.Empty == received || (old.Length == received.Length && old == received))
					return;

				Fake7.CustomSerial.Write(old = received);
			}
			catch (Exception e)
			{
				Info(rcv = "Fake8receiver():  " + e.Message + " during " + received);
			}
		}

		/// <summary>
		/// Arduino DataReceived method runs on a secondary thread from ThreadPool
		/// calls Fake8receiver() via delegate
		/// </summary>
		private void AndroidDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			SerialPort sp = (SerialPort)sender;
			while (ongoing)
			{
				try
				{
					string s = sp.ReadExisting();

					Crcv(rcv = s);	// pass current instance to Fake8receiver() delegate
				}
				catch (Exception rex)
				{
					Info(rcv = "AndroidDataReceived():  " + rex.Message );
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
			ongoing = true;
			Arduino = new SerialPort();
			cmd = new byte[4];

// read configuration properties

			string parms = pluginManager.GetPropertyValue(Fake7.Ini + "parms")?.ToString();
			Label = pluginManager.GetPropertyValue(Fake7.Ini + "rcv")?.ToString();

			if (null != parms && 0 < parms.Length && null != Label && 0 < Label.Length)
			{
				Prop = parms.Split(',');
				if (5 < Prop.Length)
				{
					string pill = pluginManager.GetPropertyValue(Fake7.Ini + "pill")?.ToString();

					b4 = new string[Prop.Length];
					for (byte i = 0; i < Prop.Length; i++)
						b4[i] = "";
					F7.AttachDelegate(Label,	() =>old);		// Fake7 sends this property to Custom Serial plugin
					F7.AttachDelegate("F8rcv",	() =>rcv);
					if (null != pill || 0 < pill.Length)
					{													// launch serial port
						Arduino.DataReceived += AndroidDataReceived;
						F7.Fopen(Arduino, pill);
					}
					else F7.Sports(Fake7.Ini + "Custom Serial 'F8pill' missing from F8.ini");
				}
				else Info($"Init():  {Fake7.Ini + "parms"}.Length {Prop.Length} < expected 6");
			}
			else Info("Init():  missing " + Fake7.Ini + "parms and/or " + Fake7.Ini + "rcv");
		}																			// Init()
	}
}
