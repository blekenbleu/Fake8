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
		}

		static SerialPort Custom;

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
			string[] namesArray = pluginManager.GetPropertyValue(Ini + "parms")?.ToString().Split(',');
			string port = pluginManager.GetPropertyValue(Ini + "com")?.ToString();

			if (null == port || 0 == port.Length)
				Sports(Ini + "com missing");
			Custom = new SerialPort(port, 9600);
			try
			{
				Custom.PortName = port;
				Custom.Open();
				Info("Init(): Found "+port);
			}
			catch (Exception ex)
			{
				Sports(port + " Open() failed.  " + ex.Message);
			}
			Custom.Close();

			Settings.CCvalue[1] = Settings.CCvalue[1];								// matters in MIDIio; not here..??
		}																			// Init()
	}
}
