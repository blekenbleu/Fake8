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

	[PluginDescription("Fake8 process SimHub Custom Serial for Arduino;  echo Arduino (hex) to SimHub Custom Serial via com0com")]
	[PluginAuthor("blekenbleu")]
	[PluginName("Fake8")]
	public class Fake8 : IPlugin, IDataPlugin
	{
		internal FakeSettings Settings;
		internal static readonly string Ini = "DataCorePlugin.ExternalScript.F8"; // configuration source

		/// <summary>
		/// wraps SimHub.Logging.Current.Info() with prefix
		/// </summary>
		internal static bool Info(string str)
		{
			SimHub.Logging.Current.Info("Fake8." + str);	// bool Info()
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
		private SerialPort CustomSerial, Arduino;		// CustomSerial is com0com to SimHub Custom Serial device

		private System.Timers.Timer aTimer;
		private void OnTimedEvent(object source, ElapsedEventArgs e)
		{
			Arduino.Open();
			Arduino.DtrEnable = Arduino.RtsEnable = true;				// tickle the Arduino
			CustomSerial.Write(Traffic[2] = "CustomSerial.Write([Arduino reset?])\n");
		}
		/// <summary>
		/// reset the Arduino
		/// </summary>
		private bool Reset(uint msec)
		{
            aTimer = new System.Timers.Timer((float)msec)
            {
                AutoReset = false                   					// one time
            };
            aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
			Arduino.DtrEnable = Arduino.RtsEnable = false;				// tickle the Arduino
			Arduino.Close();
			aTimer.Enabled = true;										// start the clock
			return false;
		}

		/// <summary>
		/// write byte pairs to Arduino based on Custom Serial messages
		/// </summary> 
		private string old;
		private void Fake8receiver(object sender, SerialDataReceivedEventArgs e)
		{
			if (CustomSerial.IsOpen)
				try
				{
					if (String.Empty != (Traffic[0] = CustomSerial.ReadExisting()) && Arduino.IsOpen)
					{
						if (old.Length == Traffic[0].Length && old == Traffic[0])
							return;
						old = Traffic[0];
						string[] parm = Traffic[0].Split(';');

						for (byte i = 0; i < parm.Length; i++)
							if (Parse(parm[i]))
							{
								Arduino.Write(now, 0, 2);
								Traffic[3] = $"Arduino.Write(now[] = '{now[0]:X},{now[1]:X}')";
							}
							else Traffic[2] = $"Parse({parm[i]}) returned false";
					}
				}
				catch
				{
					Info("Fake8receiver():  " + e.ToString());
				}
		}

		private bool Fbyte(byte n)
		{
			Traffic[2] = $"Fbyte({n}): Settings.Value[{n}] = {Settings.Value[n]}; now[1] = {now[1]}";
			if (Settings.Value[n] == now[1])
				return false;
			Settings.Value[n] = now[1];
			now[0] = (byte)(n + 0x90);
			return true;
		}

		private uint[] word;
		private bool Fword(byte n, uint v)
		{
			now[0] = (byte)((n * 5) + 0x90);
			if (word[n] == v)
				return false;
			word[n] = v;
			return true;			// for now; need a higher precision 2-byte option
		}

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

			if (2 == f8.Length)
				now[1] = (byte)(127 & (value = uint.Parse(f8[1])));
			else
			{
				Traffic[2] = $"Parse(): invalid parm '{parms}'";
				return false;
			}
			switch (f8[0])
			{
				case "f0":
					return Fword(0, value);
				case "f1":
					return Fbyte(1);
				case "f2":
					return Fbyte(2);
				case "f3":
					return Fbyte(3);
				case "f4":
					return Fbyte(4);
				case "f5":
					return Fword(1, value);
				case "f6":
					return Fbyte(6);
				case "f7":
					return Fbyte(7);
				case "f8":
					return Fbyte(8);
				case "f9":
					return Reset(value);
				default:
					Traffic[2] = $"Parse(default): unsupported parm '{parms}'";
					return false;
			}
		}
		
		private void Pillreceiver(object sender, SerialDataReceivedEventArgs e)
		{
			if (Arduino.IsOpen)
			{
				try
				{
					if (String.Empty != (Traffic[1] = Arduino.ReadExisting()) && CustomSerial.IsOpen)
					{
						CustomSerial.Write(' ' + Traffic[1]);
					}
				}
				catch
				{
					Info("Pillreceiver():  " + e.ToString());
				}
			}
		}

		/// <summary>
		/// report available serial ports
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

		/// <summary>
		/// Called one time per game data update, contains all normalized game data,
		/// raw data are intentionnally "hidden" under a generic object type (A plugin SHOULD NOT USE IT)
		///
		/// This method is on the critical path, it must execute as fast as possible and avoid throwing any error
		///
		/// </summary>
		/// <param name="pluginManager"></param>
		/// <param name="data">Current game data, including current and previous data frame.</param>
		public void DataUpdate(PluginManager pluginManager, ref GameData data)
		{
			// scan for SimHub property changes, send commands to Arduino
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
		/// Called at plugin manager stop, close/dispose anything needed here !
		/// Plugins are rebuilt at game change
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
		private void Open(SerialPort serial, string port, SerialDataReceivedEventHandler f)
		{	
			try
			{
				serial.DataReceived += f;				// set up the event before opening the serial port
				serial.PortName = port;
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

			Traffic = new string[] {"nothing yet", "still waiting", "so far, so good", "watch this space"};
			f8 = new string[] { "f?", "v?" };
			now = new byte[] { 0,0,0 };
			word = new uint[] { 0,0 };
			old = "old";

// Load settings

			Settings = this.ReadCommonSettings<FakeSettings>("GeneralSettings", () => new FakeSettings());
			Info($"Init().InitCount:  {++Settings.Value[2]}");
			for (byte i = 2; i < 9; i++)
				Attach(i);

			string parms = pluginManager.GetPropertyValue(Ini + "parms")?.ToString();
			if (null != parms && 0 < parms.Length)
				parmArray = parms.Split(',');
			else Info("Init():  null " + Ini + "parms");
			string null_modem = pluginManager.GetPropertyValue(Ini + "com")?.ToString();
			string pill = pluginManager.GetPropertyValue(Ini + "pill")?.ToString();

			if (null == null_modem || 0 == null_modem.Length)
				Sports(Ini + "Custom Serial 'F8com' missing from F8.ini");
			else
			{
				Open(CustomSerial = new SerialPort(null_modem, 9600), null_modem, Fake8receiver);
				if (null == pill || 0 == pill.Length)
					Sports(Ini + "Arduino Serial 'F8pill' missing from F8.ini");
				else Open(Arduino = new SerialPort(pill, 9600), pill, Pillreceiver);
			}
		}																			// Init()
	}
}
