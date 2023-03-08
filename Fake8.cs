using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Timers;

namespace Fake8plugin
{
	// these must be public
	public class FakeSettings // saved while plugin restarts
	{
		public byte[] Value { get; set; } = { 0,0,0,0,0,0,0,0,0,0 };
	}

	[PluginDescription("8-bit helper plugin between SimHub Custom Serial and Arduino;  echo Arduino (hex) to SimHub Custom Serial via com0com")]
	[PluginAuthor("blekenbleu")]
	[PluginName("Fake8")]
	public class Fake8 : IPlugin, IDataPlugin
	{
//		public delegate void SerialDataReceivedEventHandler(object sender, SerialDataReceivedEventArgs e);
		internal FakeSettings Settings;
		internal static readonly string Ini = "DataCorePlugin.ExternalScript.F8"; // configuration source file

		/// <summary>
		/// wraps SimHub.Logging.Current.Info() with prefix
		/// </summary>
		internal static bool Info(string str)
		{
			SimHub.Logging.Current.Info("Fake8." + str);						// bool Info()
			return true;
		}

		/// <summary>
		/// create SimHub properties
		/// </summary>
		private void Attach(byte index)
		{
			switch (index)
			{
				case 0:
					this.AttachDelegate("Fake8.Value[0]", () => Settings.Value[0]);
					break;
				case 1:
					this.AttachDelegate("Fake8.Value[1]", () => Settings.Value[1]);
					break;
				case 2:
					this.AttachDelegate("Fake8.InitCount", () => Settings.Value[2]);
					break;
				case 3:
					this.AttachDelegate("Fake8.CustomSerial.ReadExisting()", () => Traffic[0]);
					break;
				case 4:
					this.AttachDelegate("Fake8.Arduino.ReadExisting", () => Traffic[1]);
					break;
				case 5:
					this.AttachDelegate("Fake8.PluginMsg", () => Traffic[2]);
					break;
				case 6:
					this.AttachDelegate("Fake8.f8[0]", () => f8[0]);
					break;
				case 7:
					this.AttachDelegate("Fake8.f8[1]", () => f8[1]);
					break;
				case 8:
					this.AttachDelegate("Fake8.Arduino.Write()", () => Traffic[3]);
					break;
				default:
					Info($"Attach({index}): unsupported value");
					break;
			}
		}

		private byte[] now;
		private string[] Traffic;
		private SerialPort CustomSerial, Arduino;						// CustomSerial uses com0com for SimHub Custom Serial device

		private System.Timers.Timer aTimer;
		private void OnTimedEvent(object source, ElapsedEventArgs e)	// still does not restart Arduino sketch
		{
			Arduino.Open();
			Arduino.DtrEnable = Arduino.RtsEnable = true;				// tickle the Arduino
			CustomSerial.Write(Traffic[2] = "CustomSerial.Write([Arduino reset?])\n");
		}

		/// <summary>
		/// try (but fail) to restart the Arduino sketch
		/// </summary>
		private bool Reset(uint msec)
		{
			aTimer = new System.Timers.Timer((float)msec) { AutoReset = false };	// one time
			aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
			Arduino.DtrEnable = Arduino.RtsEnable = false;				// tickle the Arduino
			Arduino.Close();
			aTimer.Enabled = true;										// start the clock
			return false;
		}

		/// <summary>
		/// declare a delegate for Fake8receiver()
		/// </summary>
		private delegate void CustDel(Fake8 I, string text);
		readonly CustDel Crcv = Fake8receiver;

		private Fake8 I() { return this; }		// callback for current class instance
		private string old;
		/// <summary>
		/// Called by delegate from DataReceived method CustomDataReceived(),
		/// which runs on a secondary thread from ThreadPool, so reportedly should not directly access main thread variables
		/// As a delegate, it must be static and passed the class instance for variables... Calls Parse()
		/// </summary>
		static internal void Fake8receiver(Fake8 I, string received)
		{
			try
			{
				if (String.Empty != (I.Traffic[0] = received) && I.Arduino.IsOpen)
				{
					if (I.old.Length == I.Traffic[0].Length && I.old == I.Traffic[0])
						return;
					I.old = I.Traffic[0];
					string[] parm = I.Traffic[0].Split(';');

					for (byte i = 0; i < parm.Length; i++)
						I.Parse(parm[i]);
				}
			}
			catch (Exception e)
			{
				Info("Fake8receiver():  " + e.Message + " during " + I.Traffic[0]);
			}
			return;
		}

		/// <summary>
		/// SimHub Custom Serial DataReceived (via com0com) method runs on a secondary thread from ThreadPool
		/// calls Fake8receiver() via delegate
		/// </summary>
		private void CustomDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			SerialPort sp = (SerialPort)sender;
			string s= sp.ReadExisting();

			Crcv(I(), s);						// pass current instance to Fake8receiver() delegate
		}

		private uint[] word;					// should be in Settings

		/// <summary>
		/// match names in Custom Serial messages; pick out values
		/// </summary>
		private string[] f8;
		private bool Parse(string parms)
		{
			if (0 == parms.Length)
				return false;

			uint value;
			f8 = parms.Split('=');

			if (2 == f8.Length && 1 < f8[0].Length && 'f' == f8[0][0])
			{
				now[1] = (byte)(127 & (value = uint.Parse(f8[1])));
				byte n = byte.Parse(f8[0].Substring(1, f8[0].Length - 1));
				if (9 == n)
					return Reset(value);

				if (n >= Settings.Value.Length)
				{
					Traffic[2] = $"Parse({parms}): {n} vs {Settings.Value.Length} Settings.Value.Length";
					return false;
				}

				if (0 == n || 5 == n)
				{
					int i = (0 == n) ? 0 : 1;

					if (value == word[i])
						return false;
					word[i] = value;
				}
				else
				{
					if (Settings.Value[n] == now[1])
						return false;
					Settings.Value[n] = now[1];
				}

				now[0] = (byte)(n + 0x90);
				Arduino.Write(now, 0, 2);
				Traffic3(2);
				return true;
			}

			Traffic[2] = $"Parse(): invalid parm '{parms}'";
			return false;
		}

		/// <summary>
		/// Arduino DataReceived method runs on a secondary thread from ThreadPool
		/// </summary>
		private void Pillreceiver(object sender, SerialDataReceivedEventArgs e)
		{
			if (Arduino.IsOpen)
			{
				try
				{
					if (String.Empty != (Traffic[1] = Arduino.ReadExisting()) && CustomSerial.IsOpen)
						CustomSerial.Write(' ' + Traffic[1]);
				}
				catch
				{
					Info("Pillreceiver():  " + e.ToString());
				}
			}
		}

		/// <summary>
		/// report known serial ports
		/// </summary>
		private void Sports(string n)
		{
			string s = $"Open(): {n};  available serial ports:";

			foreach (string p in SerialPort.GetPortNames())
				s += "\n\t" + p;

			Info(s);
		}

		/// <summary>
		/// Instance of the current plugin manager
		/// </summary>
		public PluginManager PluginManager { get; set; }

		private void Traffic3(byte ct)
		{
			Traffic[3] = $"Arduino.Write(now[] = '{now[0]:X}'";
			for (byte i = 1; i < ct; i++)
				Traffic[3] += $",'{now[0]:X}'";
			Traffic[3] += ")";
		}

		/// <summary>
		/// Called one time per game data update, contains all normalized game data,
		/// raw data are intentionnally "hidden" under a generic object type (A plugin SHOULD NOT USE IT)
		///
		/// This method is on the critical path, it must execute as fast as possible and avoid throwing any error
		///
		/// </summary>
		/// <param name="pluginManager"></param>
		/// <param name="data">Current game data, including current and previous data frame.</param>
		private string reset;
		public void DataUpdate(PluginManager pluginManager, ref GameData data)
		{
			// scan for SimHub property changes, send commands to Arduino
			string button = pluginManager.GetPropertyValue(Ini + "reset")?.ToString();

			if (0 < button.Length && button != "0" && button != reset)
			{
				now[0] = (byte)(0xFB);			// send only if changed and not "0";
				Arduino.Write(now, 0, 1);
				Traffic3(1);
			}
			reset = button;
			return;
		}

		/// <summary>
		/// Called by End() to close a serial port
		/// </summary>
		private void Close(SerialPort serial)
		{
			if (serial.IsOpen)
				try
				{
					serial.Close();
					serial.DiscardInBuffer();
					serial.DiscardOutBuffer();
				}
				catch {/* ignore */}
		}

		/// <summary>
		/// Called at plugin manager stop, close/dispose anything needed here!
		/// Plugins are rebuilt at game changes, but NCalc files are not re-read
		/// </summary>
		/// <param name="pluginManager"></param>
		public void End(PluginManager pluginManager)
		{
			string s = "End():\n";

			for (byte i = 0; i < Settings.Value.Length; i++)
				s += $"\tValue[{i}]:  {Settings.Value[i]}\n";

			Info(s);

			this.SaveCommonSettings("GeneralSettings", Settings);
			Close(CustomSerial);
			Close(Arduino);
		}

		/// <summary>
		/// Called by Init() to open a serial port
		/// </summary>
		private void Fopen(SerialPort serial, string port)
		{	
			try
			{
				serial.PortName = port;
				serial.BaudRate = 9600;
				serial.DtrEnable = true;				// nothing received from Arduino without this
  				serial.RtsEnable = true;
  				serial.Handshake = Handshake.None;
				serial.Open();
				Info($"Open({port}): success!");
			}
			catch (Exception ex)
			{
				Sports($"Open({port}) failed.  " + ex.Message);
			}
		}

		/// <summary>
		/// Called at SimHub start then after game changes
		/// </summary>
		/// <param name="pluginManager"></param>
		public void Init(PluginManager pluginManager)
		{
			string[] parmArray;

			CustomSerial = new SerialPort();
			Arduino = new SerialPort();
			Traffic = new string[] {"nothing yet", "still waiting", "so far, so good", "watch this space"};
			f8 = new string[] { "f?", "v?" };
			now = new byte[] { 0,0,0 };
			word = new uint[] { 0,0 };
			old = "old";
			reset = "0";

// Load settings, create properties

			Settings = this.ReadCommonSettings<FakeSettings>("GeneralSettings", () => new FakeSettings());

			Info($"Init().InitCount:  {++Settings.Value[2]}; Settings.Length = {Settings.Value.Length}");
			if (8 > Settings.Value.Length)
				Settings = new FakeSettings();
			for (byte i = 2; i < 9; i++)
				Attach(i);

// read configuration properties

			string parms = pluginManager.GetPropertyValue(Ini + "parms")?.ToString();

			if (null != parms && 0 < parms.Length)
				parmArray = parms.Split(',');
			else Info("Init():  missing " + Ini + "parms");

// launch serial ports

			string null_modem = pluginManager.GetPropertyValue(Ini + "com")?.ToString();
			string pill = pluginManager.GetPropertyValue(Ini + "pill")?.ToString();

			if (null == null_modem || 0 == null_modem.Length)
				Sports(Ini + "Custom Serial 'F8com' missing from F8.ini");
			else
			{
				CustomSerial.DataReceived += CustomDataReceived;
				Fopen(CustomSerial, null_modem);
				if (null == pill || 0 == pill.Length)
					Sports(Ini + "Arduino Serial 'F8pill' missing from F8.ini");
				else
				{
					Arduino.DataReceived += Pillreceiver;
					Fopen(Arduino, pill);
				}
			}
		}																			// Init()
	}
}
