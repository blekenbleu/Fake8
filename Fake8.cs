using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.IO;
using System.IO.Ports;
using System.Threading;

namespace Fake8plugin
{
	// these must be public
	public class MysterySettings // saved while plugin restarts
	{
		public byte[] CCvalue { get; set; } = { 0,0,0 };
	}

	[PluginDescription("Fake8 echo SimHub Custom Serial to Arduino;  Arduino (hex) to SimHub Custom Serial via com0com")]
	[PluginAuthor("blekenbleu")]
	[PluginName("Fake8")]
	public class Fake8 : IPlugin, IDataPlugin
	{
		internal MysterySettings Settings;
		internal static readonly string Ini = "DataCorePlugin.ExternalScript.F8"; // configuration source


		/// <summary>
		/// wraps SimHub.Logging.Current.Info() with prefix
		/// </summary>
		internal static bool Info(string str)
		{
			SimHub.Logging.Current.Info("Fake8." + str);	// bool Info()
			return true;
		}

		private byte[] now;
		private string[] Traffic;

		/// <summary>
		/// create SimHub properties
		/// </summary> 
		private void Attach(byte index)
		{
			switch (index)
			{
				case 0:
					this.AttachDelegate("Fake8.Open()?", () => Settings.CCvalue[0]);
					break;
				case 1:
					this.AttachDelegate("Fake8.got?", () => Settings.CCvalue[1]);
					break;
				case 2:
					this.AttachDelegate("Fake8.InitCount", () => Settings.CCvalue[2]);
					break;
				case 3:
					this.AttachDelegate("Fake8.CustomMsg", () => Traffic[0]);
					break;
				case 4:
					this.AttachDelegate("Fake8.ArduinoMsg", () => Traffic[1]);
					break;
				default:
					Info($"Attach({index}): unsupported value");
					break;
			}
		}

		static SerialPort Custom, Arduino;		// Custom is com0com to SimHub Custom Serial device

		private void Fake8receiver(object sender, SerialDataReceivedEventArgs e)
		{
			if (Custom.IsOpen)
				try
				{
					if (String.Empty != (Traffic[0] = Custom.ReadExisting()) && Arduino.IsOpen)
						Arduino.Write(Traffic[0]);
				}
				catch
				{
					Info("Fake8receiver():  " + e.ToString());
				}
		}

		private char[] buffer;
		private void Pillreceiver(object sender, SerialDataReceivedEventArgs e)
		{
			if (Arduino.IsOpen)
			{
				Settings.CCvalue[0] = 1;
				try
				{
					int count;
					bool got = false;

					Settings.CCvalue[1] = 0;
					while (0 < (count = Arduino.BytesToRead))
					{
						int now = (count > buffer.Length) ? buffer.Length : count;
						now = Arduino.Read(buffer, 0, now);
						if (0 < now)
						{
							got = true;
							Settings.CCvalue[1] = 1;
							Custom.Write(Traffic[1] = (new string(buffer).Substring(0, now)));
						}
					}
					if (got)
						Custom.Write("\n");
				}
				catch
				{
					Info("Pillreceiver():  " + e.ToString());
				}
			}
			else Settings.CCvalue[0] = 0;
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

			for (byte i = 0; i < now.Length; i++)
				if (now[i] != Settings.CCvalue[i])
				{
					s += $"\tCCvalue[{i}] changed from {now[i]} to {Settings.CCvalue[i]}\n";
					now[i] = Settings.CCvalue[i];
				}

			if (8 < s.Length)
				Info(s);

			Settings.CCvalue[0] = 5;
			Settings.CCvalue[1] = 6;
			this.SaveCommonSettings("GeneralSettings", Settings);
			Close(Custom);
			Close(Arduino);
		}

		/// <summary>
		/// Called by Init() to open a serial port
		/// </summary>
		private void Open(SerialPort serial, string port, SerialDataReceivedEventHandler f)
		{	
			try
			{
				serial.DataReceived += f;		// set up the event before opening the serial port
				serial.PortName = port;
				serial.Open();
				Info("Open(): Found " + port);
			}
			catch (Exception ex)
			{
				Sports($"Open({port}) failed.  " + ex.Message);
			}
		}

		private void OpenPill(string port)
		{	
			Arduino = new SerialPort(port, 9600);
			try
			{
				Arduino.DataReceived +=Pillreceiver;		// set up the event before opening the Arduino port
				Arduino.PortName = port;
//				Arduino.RtsEnable = true;
				Arduino.DtrEnable = true;					// default without this was nothing received
//				Arduino.Handshake = Handshake.None;
				Arduino.Open();
				Info("OpenPill(): Found " + port);
			}
			catch (Exception ex)
			{
				Sports($"OpenPill({port}) failed.  " + ex.Message);
			}
		}

		/// <summary>
		/// Called at SimHub start then after game changes
		/// </summary>
		/// <param name="pluginManager"></param>
		public void Init(PluginManager pluginManager)
		{
			string[] parmArray;
			buffer = new char[16];

			Traffic = new string[] {"nothing yet", "still waiting"};
			now = new byte[] { 0,0,0 };

// Load settings
			Settings = this.ReadCommonSettings<MysterySettings>("GeneralSettings", () => new MysterySettings());
			Info($"Init().InitCount:  {++Settings.CCvalue[2]}");
			for (byte i = 0; i < 5; i++)
				Attach(i);

			string parms = pluginManager.GetPropertyValue(Ini + "parms")?.ToString();
			if (null != parms && 0 < parms.Length)
				parmArray = parms.Split(',');
			else Info("Init():  null " + Ini + "parms");
			string port = pluginManager.GetPropertyValue(Ini + "com")?.ToString();
			string pill = pluginManager.GetPropertyValue(Ini + "pill")?.ToString();

			if (null == port || 0 == port.Length)
				Sports(Ini + "Custom Serial 'F8com' missing from F8.ini");
			else
			{
				Open(Custom = new SerialPort(port, 9600), port, Fake8receiver);
				if (null == pill || 0 == pill.Length)
					Sports(Ini + "Arduino Serial 'F8pill' missing from F8.ini");
				else OpenPill(pill);
			}

			Settings.CCvalue[1] = Settings.CCvalue[1];								// matters in MIDIio; not here..??
		}																			// Init()
	}
}
