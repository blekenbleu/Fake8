using GameReaderCommon;
using SimHub.Plugins;
using System;
using System.IO;
using System.IO.Ports;

namespace Fake8plugin
{
	// these must be public
	public class FakeSettings // saved while plugin restarts
	{
		public byte[] Value { get; set; } = { 0,0,0 };
		public string[] Prop { get; set; } = { "","","","","","","","","","" };
	}

	[PluginDescription("Custom Serial helper creates properties from messages")]
	[PluginAuthor("blekenbleu")]
	[PluginName("Fake8")]
	public class Fake8 : IPlugin, IDataPlugin
	{
		internal FakeSettings Settings;
		internal static readonly string Ini = "DataCorePlugin.ExternalScript.Fake8"; // configuration source file
		SerialPort CustomSerial;						// CustomSerial uses com0com for SimHub Custom Serial device
		private string[] Label, f8;
		private string FromArduino, rcv;

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
		private void Attach()
		{
			this.AttachDelegate(Label[0],	 () => Settings.Prop[0]);
			this.AttachDelegate(Label[1],	 () => Settings.Prop[1]);
			this.AttachDelegate(Label[2],	 () => Settings.Prop[2]);
			this.AttachDelegate(Label[3],	 () => Settings.Prop[3]);
			this.AttachDelegate(Label[4],	 () => Settings.Prop[4]);
			this.AttachDelegate(Label[5],	 () => Settings.Prop[5]);
			this.AttachDelegate(Label[6],	 () => Settings.Prop[6]);
			this.AttachDelegate(Label[7],	 () => Settings.Prop[7]);
			this.AttachDelegate(Label[8],	 () => Settings.Prop[8]);
			this.AttachDelegate(Label[9],	 () => Settings.Prop[9]);
		}

		/// <summary>
		/// Runs on a secondary thread
		/// </summary>
		internal void Fake8receiver(object sender, SerialDataReceivedEventArgs e)
		{
			string received = "";

			try
			{
				SerialPort sp = (SerialPort)sender;
				received = sp.ReadExisting();
				if (String.Empty != received)
				{
					string[] parm = received.Split(';');

					for (byte i = 0; i < parm.Length; i++)
						if (0 < parm[i].Length)
						{
							f8 = parm[i].Split('=');
							if (2 == f8.Length && 0 < f8[0].Length && 0 < f8[1].Length)
								for (byte j = 0; j < Label.Length; j++)
									if (Label[i] == f8[0])
										Settings.Prop[i] = f8[1];
							return;
						}
						else
						{
							CustomSerial.Write((Settings.Prop[8] = $"Invalid parm msg: '{received}'") + "\n");
							Settings.Prop[8] += "sent";
						}
				}
			}
			catch (Exception ex)
			{
				Info("Fake8receiver():  " + ex.Message + " during " + received);
			}
			return;
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
			if (null == FromArduino || 0 == FromArduino.Length)
				return;

			string Arduino = pluginManager.GetPropertyValue(FromArduino)?.ToString();

			if (null != Arduino && 0 < Arduino.Length && Arduino != rcv)
			{
				CustomSerial.Write(Settings.Prop[8] = rcv = Arduino);
				Settings.Prop[8] += "sent";
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
//				serial.RtsEnable = true;
//				serial.Handshake = Handshake.RequestToSend;
				serial.ReadTimeout = 500;
				serial.WriteTimeout = 500;
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
			CustomSerial = new SerialPort();
			rcv = "\n";

// Load settings, create properties

			Settings = this.ReadCommonSettings<FakeSettings>("GeneralSettings", () => new FakeSettings());

			Info($"Init().InitCount:  {++Settings.Value[2]}; Settings.Length = {Settings.Value.Length}");
			if (3  != Settings.Value.Length)
				Settings = new FakeSettings();
			Label = new string[Settings.Prop.Length];

// read configured property names

			string parms = pluginManager.GetPropertyValue(Ini + "parms")?.ToString();
			string[] split;
			byte i = 0, n;

			if (null == parms || 0 == parms.Length)
				Info("Init():  missing " + Ini + "parms");
			else
			{
				split = parms.Split(',');
				if (split.Length > Label.Length)
					Info($"Init(): {split.Length} parms vs {Label.Length} Label.Length\n");
				n = (byte)((split.Length < Label.Length) ? split.Length : Label.Length);
				for (i = 0; i < n; i++)
					Label[i] = split[i];
			}
			for (; i < Label.Length; i++)
				Label[i] = "f" + i;
			Attach();

			FromArduino = pluginManager.GetPropertyValue(Ini + "rcv")?.ToString();
			if (null == FromArduino || 0 == FromArduino.Length)
				Info("Init(): missing " + Ini + "rcv");

// launch serial ports

			string null_modem = pluginManager.GetPropertyValue(Ini + "com")?.ToString();

			if (null == null_modem || 0 == null_modem.Length)
				Sports(Ini + "Custom Serial 'Fake8com' missing from F8.ini");
			else
			{
				CustomSerial.DataReceived += Fake8receiver;
				CustomSerial.NewLine = ";";
				Fopen(CustomSerial, null_modem);
			}
		}																			// Init()
	}
}
