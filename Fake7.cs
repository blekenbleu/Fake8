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
		public byte[] Value { get; set; } = { 0 };
		public string[] Prop { get; set; } = { "","","","","","","","","","" };
	}

	[PluginDescription("fake serial device plugin to SimHub Custom Serial via com0com null modem")]
	[PluginAuthor("blekenbleu")]
	[PluginName("Fake8")]
	public class Fake7 : IPlugin, IDataPlugin
	{
		private FakeSettings Settings;
		private Fake8 F8;
		private static readonly string Ini = "DataCorePlugin.ExternalScript.Fake8";	// configuration source file
		private string[] Msg, Label;
		private string b4;
		private SerialPort CustomSerial;								// SimHub Custom Serial device via com0com

		/// <summary>
		/// wraps SimHub.Logging.Current.Info() with prefix
		/// </summary>
		private static bool Info(string str)
		{
			SimHub.Logging.Current.Info("Fake7." + str);								// bool Info()
			return true;
		}

		/// <summary>
		/// create SimHub properties
		/// </summary>
		private void Attach()
		{
			this.AttachDelegate("InitCount",		() => Settings.Value[0]);
			this.AttachDelegate("ReadExisting()",	() => Msg[0]);
			this.AttachDelegate("PluginMsg",		() => Msg[1]);
			this.AttachDelegate(Label[0],			() => Settings.Prop[0]);
			this.AttachDelegate(Label[1],			() => Settings.Prop[1]);
			this.AttachDelegate(Label[2],			() => Settings.Prop[2]);
			this.AttachDelegate(Label[3],			() => Settings.Prop[3]);
			this.AttachDelegate(Label[4],			() => Settings.Prop[4]);
			this.AttachDelegate(Label[5],			() => Settings.Prop[5]);
			this.AttachDelegate(Label[6],			() => Settings.Prop[6]);
			this.AttachDelegate(Label[7],			() => Settings.Prop[7]);
			this.AttachDelegate(Label[8],			() => Settings.Prop[8]);
			this.AttachDelegate(Label[9],			() => Settings.Prop[9]);
		}

		/// <summary>
		/// match Custom Serial names; save settings
		/// </summary>
		private bool Parse(string parms)
		{
			string[] f8 = parms.Split('=');

			if (2 == f8.Length && 1 < f8[0].Length)
			{
				for (byte i = 0; i < Settings.Prop.Length; i++)
					if (f8[0] == Label[i])
					{
						Settings.Prop[i] = f8[1];
						return true;
					}
				Msg[1] = "Parse(): no match for " + parms;
			}
			else Msg[1] = $"Parse(): invalid parm '{parms}'";
			return false;
		}

		/// <summary>
		/// declare a delegate for Fake7receiver()
		/// </summary>
		private delegate void CustDel(Fake7 I, string text);
		readonly CustDel Crcv = Fake7receiver;

		private Fake7 I() { return this; }		// callback for current class instance
		/// <summary>
		/// Called by delegate from DataReceived method CustomDataReceived(),
		/// which runs on a secondary thread from ThreadPool;  should not directly access main thread variables
		/// As a delegate, it must be static and passed the class variables instance... Calls Parse()
		/// </summary>
		static string old;
		static private void Fake7receiver(Fake7 I, string received)
		{

			try
			{
				if (String.Empty == (I.Msg[0] = received)
				 || (old.Length == received.Length && old == received))
					return;

				old = received;
				string[] parm = received.Split(';');

				for (byte i = 0; i < parm.Length; i++)
					if (0 < parm[i].Length)
						I.Parse(parm[i]);
			}
			catch (Exception e)
			{
				Info("Fake7receiver():  " + e.Message + " during " + received);
			}
		}

		/// <summary>
		/// SimHub Custom Serial DataReceived (via com0com) method runs on a secondary thread from ThreadPool
		/// calls Fake7receiver() via delegate
		/// </summary>
		private void CustomDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			SerialPort sp = (SerialPort)sender;
			string s= sp.ReadExisting();

			Crcv(I(), s);						// pass current instance to Fake7receiver() delegate
		}

		/// <summary>
		/// Log string of known serial ports
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
			F8.Run(pluginManager);		// property changes drive Arduino

			string prop = pluginManager.GetPropertyValue(F8.Ini + F8.Label)

			if (null != prop && 0 < prop.Length && (prop.Length != b4.Length || b4 != prop))
			{
				b4 = prop;
				CustomSerial.Write(prop);
			}
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
			this.SaveCommonSettings("GeneralSettings", Settings);
			Close(CustomSerial);
			F8.End(this);
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

			b4 = old = "old";
			CustomSerial = new SerialPort();
			Msg = new string[] {"nothing yet", "so far, so good"};

// Load settings, create properties

			Settings = this.ReadCommonSettings<FakeSettings>("GeneralSettings", () => new FakeSettings());

			Info($"Init().InitCount:  {++Settings.Value[0]}; Settings.Length = {Settings.Value.Length}");
			if (10 > Settings.Prop.Length)
				Settings = new FakeSettings();
			Label = new string[Settings.Prop.Length];

// read configuration properties

			string parms = pluginManager.GetPropertyValue(Ini + "parms")?.ToString();
			byte i = 0;

			if (null == parms || 0 == parms.Length)
				Info("Init():  missing " + Ini + "parms");
			else
			{
				parmArray = parms.Split(',');
				if (parmArray.Length > Settings.Prop.Length)
					Info($"Init():  {Ini + "parms"}.Length {parmArray.Length} > {Settings.Prop.Length}");
				else {
					int n = (parmArray.Length < Settings.Prop.Length) ? parmArray.Length : Settings.Prop.Length;

					for (i = 0; i < n; i++)
						Label[i] = parmArray[i];
				}
			}
			for (; i < Settings.Prop.Length; i++)
					Label[i] = "f" + i;
			Attach();

			// launch serial ports

			string null_modem = pluginManager.GetPropertyValue(Ini + "com")?.ToString();

			if (null == null_modem || 0 == null_modem.Length)
				Sports(Ini + "Custom Serial 'F8com' missing from F8.ini");
			else
			{
				CustomSerial.DataReceived += CustomDataReceived;
				Fopen(CustomSerial, null_modem);
				F8 = new Fake8();
				F8.Init(this);
			}
		}																			// Init()
	}
}
