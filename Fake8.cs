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

	[PluginDescription("Fake8 0 value properties from ReadCommonSettings()")]
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

		/// <summary>
		/// create SimHub properties
		/// </summary> 
		private void Attach(byte index)
		{
			switch (index)
			{
				case 0:
					this.AttachDelegate("expect5", () => Settings.CCvalue[0]);
					break;
				case 1:
					this.AttachDelegate("expect6", () => Settings.CCvalue[1]);
					break;
				case 2:
					this.AttachDelegate("Fake8.InitCount", () => Settings.CCvalue[2]);
					break;
				default:
					Info($"Attach({index}): unsupported value");
					break;
			}
		}

		static SerialPort Custom;		// com0com to SimHub Custom Serial device

		/// <summary>
		/// add a delegate to act on SerialDataReceived event
		/// </summary>
		delegate void EchoCallback(string msg);
		string /* Incoming = String.Empty, */ EventMsg = String.Empty;

		private void MsgEcho(string msg)		// actual delegate
		{
			Custom.Write(msg);
		}
		private void Fake8receiver(object sender, SerialDataReceivedEventArgs e)
		{
			if (Custom.IsOpen)
				try
				{
					if (String.Empty != (EventMsg = Custom.ReadExisting()))
						MsgEcho(EventMsg);
//						this.BeginInvoke(new EchoCallback(MsgEcho), new object[] { EventMsg });
				}
				catch
				{
					Info("Fake8receiver():  " + e.ToString());
				}
		}

		/// <summary>
		/// report available serial ports
		/// </summary> 
		private void Sports(string n)
		{
			string s = $"Init() {n};  InitCount:  {++Settings.CCvalue[2]};  available serial ports:";

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

			if (Custom.IsOpen)
				try
				{
					Custom.Close();
					Custom.DiscardInBuffer();
					Custom.DiscardOutBuffer();
				}
				catch {/* ignore */}
		}

		/// Called at SimHub start then after game changes
		/// </summary>
		/// <param name="pluginManager"></param>
		public void Init(PluginManager pluginManager)
		{

			now = new byte[] { 0,0,0 };
// Load settings
			Settings = this.ReadCommonSettings<MysterySettings>("GeneralSettings", () => new MysterySettings());
			Attach(0);
			Attach(1);
			Attach(2);
			string[] namesArray;

			string parms = pluginManager.GetPropertyValue(Ini + "parms")?.ToString();
			if (null != parms && 0 < parms.Length)
				namesArray = parms.Split(',');
			else Info("Init():  null " + Ini + "parms");
			string port = pluginManager.GetPropertyValue(Ini + "com")?.ToString();

			if (null == port || 0 == port.Length)
				Sports(Ini + "com missing");
			Custom = new SerialPort(port, 9600);
			try
			{
				Custom.DataReceived += Fake8receiver;		// set up the event before opening the serial port
				Custom.PortName = port;
				Custom.Open();
				Info("Init(): Found "+port);
			}
			catch (Exception ex)
			{
				Sports(port + " Open() failed.  " + ex.Message);
			}

			Settings.CCvalue[1] = Settings.CCvalue[1];								// matters in MIDIio; not here..??
		}																			// Init()
	}
}
