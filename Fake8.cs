using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace Fake8plugin
{
	// these must be public
	public class FakeSettings // saved while plugin restarts
	{
		public byte[] Value { get; set; } = { 0,0,0 };
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
					this.AttachDelegate("Fake8.Open()?", () => Settings.Value[0]);
					break;
				case 1:
					this.AttachDelegate("Fake8.got?", () => Settings.Value[1]);
					break;
				case 2:
					this.AttachDelegate("Fake8.InitCount", () => Settings.Value[2]);
					break;
				case 3:
					this.AttachDelegate("Fake8.CustomMsg", () => Traffic[0]);
					break;
				case 4:
					this.AttachDelegate("Fake8.ArduinoMsg", () => Traffic[1]);
					break;
				case 5:
					this.AttachDelegate("Fake8.InvalidMsg", () => Traffic[2]);
					break;
				default:
					Info($"Attach({index}): unsupported value");
					break;
			}
		}

		private byte[] now;
		private string[] Traffic;
		private SerialPort CustomSerial, Arduino;		// CustomSerial is com0com to SimHub Custom Serial device

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
								Arduino.Write(now, 0, 2);
					}
				}
				catch
				{
					Info("Fake8receiver():  " + e.ToString());
				}
		}

		/// <summary>
		/// match names in Custom Serial messages; pick out values
		/// </summary> 
		private bool Parse(string parms)
		{
			if (0 == parms.Length)
				return false;

			string[] f8 = parms.Split('=');

			if (2 == f8.Length)
				now[1] = (byte)(127 & byte.Parse(f8[1]));
			else
			{
				Traffic[2] = $"Parse(): invalid parm '{parms}'";
				return false;
			}
			switch (f8[0])
			{
				case "f0":
					now[0] = 0x90;
					break;
				case "f1":
					now[0] = 0x91;
					break;
				case "f2":
					now[0] = 0x92;
					break;
				case "f3":
					now[0] = 0x93;
					break;
				case "f4":
					now[0] = 0x94;
					break;
				case "f5":
					now[0] = 0x95;
					break;
				case "f6":
					now[0] = 0x96;
					break;
				case "f7":
					now[0] = 0x97;
					break;
				default:
					Traffic[2] = $"Parse(default): unsupported parm '{parms}'";
					return false;
			}
			return true;
		}
		
		private void Pillreceiver(object sender, SerialDataReceivedEventArgs e)
		{
			if (Arduino.IsOpen)
			{
				Settings.Value[0] = 1;
				try
				{
					Settings.Value[1] = 0;
					if (String.Empty != (Traffic[1] = Arduino.ReadExisting()) && CustomSerial.IsOpen)
					{
						Settings.Value[1] = 1;
						CustomSerial.Write(' ' + Traffic[1]);
					}
				}
				catch
				{
					Info("Pillreceiver():  " + e.ToString());
				}
			}
			else Settings.Value[0] = 0;
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
//				serial.RtsEnable = true;
//				serial.Handshake = Handshake.None;
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

			Traffic = new string[] {"nothing yet", "still waiting", "so far, so good"};
			now = new byte[] { 0,0,0 };
			old = "old";

// Load settings

			Settings = this.ReadCommonSettings<FakeSettings>("GeneralSettings", () => new FakeSettings());
			Info($"Init().InitCount:  {++Settings.Value[2]}");
			for (byte i = 0; i < 6; i++)
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

			Settings.Value[1] = Settings.Value[1];								// matters in MIDIio; not here..??
		}																			// Init()
	}
}
