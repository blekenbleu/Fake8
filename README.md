
---
Fake8:&nbsp; SimHub 8-bit serial plugin
---

As noted in [Arduino for STM32 Black 'n Blue Pills, ESP32-S[2,3] ](https://blekenbleu.github.io/Arduino/),  
 [SimHub's **Custom Serial devices** plugin](https://github.com/SHWotever/SimHub/wiki/Custom-serial-devices) has limitations
- SimHub plugin Javascript is relatively inefficient, hard to debug and maintain.
- Plugin can log but not process received serial port messages from e.g. from Arduino.
- Serial data is limited to 7 bits per character.

This `Fake8` SimHub C# plugin connects to an STM32 Arduino USB COM port,
using 8 bit characters and SimHub properties, some of which are from SimHub's Custom Serial device plugin messages  
via a second serial port and  **signed** [virtual com0com Null-modem](https://pete.akeo.ie/2011/07/com0com-signed-drivers.html).  
This leverages the **SimHub Custom Serial devices** plugin user interface:  
![](Fake8.png)  
... while most heavy lifting gets done by this `Fake8` plugin.  
Sadly, `Custom Serial devices` user interface Settings are local to that plugin and inaccessible elsewhere.  
Consequently, [a `Custom Serial devices` profile](https://raw.githubusercontent.com/blekenbleu/SimHub-profiles/main/Fake8.shsds)
 sends control settings for processing by this plugin,  
via a `COM8 com0com` null modem Serial port.  
Overhead is minimized by using simple [NCalc](https://github.com/SHWotever/ncalc) expressions to generate setting change messages.  
Unlike JavaScript, NCalc Update messages repeat even if unchanged unless explicitly conditional
 by [change()](https://github.com/SHWotever/SimHub/wiki/NCalc-scripting).  
Incoming `COM8` serial data to SimHub's **Custom Serial** plugin may combine Arduino and `Fake8` strings.

Inspired by MIDI, [**`Fake8` to Arduino 8-bit protocol supports 73 commands**](https://github.com/blekenbleu/Arduino-Blue-Pill/blob/main/8-bit.md):  
- For [re]synchronization, only the first byte of each command has msb == 1
- For 63 of 73 commands. 5 lsb of first byte is an Arduino sketch-specific command:  
  - setting e.g. **PWM** parameters:&nbsp; (frequency, % range, predistortion, pin or clock number)
  - second byte is either 7-bit data or count for appended 7-bit byte array of values.  
    - One array command echoes that array.  
    - One non-array command echoes that second byte.  
- For 3-byte commands, 2 lsb of first byte are 2 msb of 16-bit data.
- For a single 2-byte command, 5 lsb of first byte are 5 msb of 12-bit data.
- A single 1-byte command restarts Arduino run-time sketch.

### Status 3 Mar 2023
- plugin communicates both with SimHub Custom Serial plugin (via com0com) and STM32 Arduino
   - current Arduino sketch merely echos ASCII hex for received bytes, confirming 8-bit communications
- next step will be adding configurable PWM to the Arduino sketch  
  for e.g. PC fans and [**Direct Drive harness tension**](https://github.com/blekenbleu/Direct-Drive-harness-tension-tester) testing.
### Status 8 Mar 2023
- skeletal [PWM_FullConfiguration](https://github.com/blekenbleu/Arduino-Blue-Pill/tree/main/PWM_FullConfiguration) sketch implemented, along with F8reset in `Fake8.cs`  

### Status 9 Mar 2023
- unable to get both COM ports working robustly in a single class.   
  Simply create properties from Custom Serial port messages in `Fake7` plugin;  
  then, use those properties in `Fake8` plugin for Arduino.  
  This will not impact game latency, since telemetry will not come thru Custom Serial plugin.
- look into [building both plugins in a single project](https://stackoverflow.com/questions/3867113/visual-studio-one-project-with-several-dlls-as-output)  
  [**search results**](https://duckduckgo.com/?q=visual+studio+multiple+%22dlls%22+in+one+solution)

### Status 10 Mar 2023
- The problem is Fake7 hanging on write back to Custom Serial via com0com;   
  Read works ok, and and both Read and Write work to e.g. Arduio Serial Monitor.  
  Changed F8.ini `Fake8rcv` setting to `f9` from `Arduino`, so that Fake7 could read a property that changes without Fake8.

### Status 11 Mar 2023
- Confirmed that Custom Serial `Incoming serial data` and com0com are by default incompatible;  
  FWIW, Arduino serial terminal works fine on COM8 instead of SimHub's Custom Serial device...??!!  
  finally got beyond `CustomSerial.Write(prop);` timeout by forcing com0com setting:  
  `change CNCB0 PortName=COM2,EmuOverrun=yes,ExclusiveMode=no,cts=on,dsr=on,dcd=on`  
  `EmuOverrun=yes` by itself did not suffice;&nbsp;  isolating essential setting is low priority.  
- Both ports work;&nbsp; Fake8receiver() can call Fake7.CustomSerial.Write(),  
  but needs exception handling (e.g. reopen)

### Status 12 Mar 2023
- Recover Arduino USM COM disconnect/reconnect events;  similar code for com0com implemented but untested.

### Status 20 Mar 2023
- Delegate fork merged to main.&nbsp; Begin scheming for [Bresenham PWM modulation](Bresenham.md)  

## Problems encountered
- SourceForge's `com0com` virtual null modem package **does not work on recent Windows 10 versions**.
   - get [Pete Batard's](https://pete.akeo.ie/2011/07/com0com-signed-drivers.html) **signed** [`com0com` driver](https://files.akeo.ie/blog/com0com.7z).
   - an alternative `may be` [test-signing](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/the-testsigning-boot-configuration-option) the SourceForge `com0com` driver.
- Trying to use `com0com` virtual COM ports in C# **fails** *unless its PortName begins with* `COM`.  
    - Free 'busy' COM port numbers using [COM Name Arbiter Tool](https://www.uwe-sieber.de/misc_tools_e.html#com_ports)  
       Run as Adminstrator, uncheck wanted and currently unused ports:  
       ![](Arbiter.png)  
- `Arduino.DtrEnable = true;` is required [for C# to read from Arduino](https://forum.arduino.cc/t/serial-communication-with-c-program-serialdatareceivedeventhandler-doesnt-work/108564/3), but not for com0com.
- Unable to restart Arduino sketch by toggling `Arduino.DtrEnable` and `Arduino.RtsEnable`.
- Unable to get both COM ports working robustly in a single class.
- Working example using delegate for data from C# Serial Port `DataReceived` thread to invoking thread.
- SimHub Custom Serial device receiving (**Incoming serial data**) seems uniquely incompatible with `com0com`.  

## Configure a [`com0com` virtual null modem](https://files.akeo.ie/blog/com0com.7z)
- Run as Adminstrator `com0com\setupc.exe`: &nbsp;   (see [com0com ReadMe](https://raw.githubusercontent.com/paulakg4/com0com/master/ReadMe) for instructions)
```
command> change CNCB0 PortName=COM2
       CNCA8 PortName=-
       CNCB8 PortName=-
       CNCA0 PortName=FAKE8
       CNCB0 PortName=SIM8
change CNCB0 PortName=COM2
Restarted CNCB0 com0com\port \Device\com0com20
ComDB: COM2 - logged as "in use"
command> change CNCB0 ExclusiveMode=yes
       CNCA8 PortName=-
       CNCB8 PortName=-
       CNCA0 PortName=FAKE8
       CNCB0 PortName=COM2
change CNCB0 PortName=COM2,ExclusiveMode=yes
Restarted CNCB0 com0com\port \Device\com0com20
command> change CNCA0 PortName=COM8
       CNCA8 PortName=-
       CNCB8 PortName=-
       CNCA0 PortName=FAKE8
change CNCA0 PortName=COM8
Restarted CNCA0 com0com\port \Device\com0com10
       CNCB0 PortName=COM2,ExclusiveMode=yes
ComDB: COM8 - logged as "in use"
command> change CNCA0 PlugInMode=yes
       CNCA8 PortName=-
       CNCB8 PortName=-
       CNCA0 PortName=COM8
change CNCA0 PortName=COM8,PlugInMode=yes
Restarted CNCA0 com0com\port \Device\com0com10
       CNCB0 PortName=COM2,ExclusiveMode=yes
command> list
       CNCA8 PortName=-
       CNCB8 PortName=-
       CNCA0 PortName=COM8,PlugInMode=yes
       CNCB0 PortName=COM2,ExclusiveMode=yes
```
Seemingly, `PlugInMode=yes` and `ExclusiveMode=yes` make no difference..

### marginally useful C# serial port references

- [Serial Port Communication](https://www.codeproject.com/Tips/361285/Serial-Port-Communication) *codeproject.com* "Although the code is self explanatory, I will explain little."
  - uses `Invoke()`; closes serial port after each read
- [Serial Comms in C# for Beginners](https://www.codeproject.com/Articles/678025/Serial-Comms-in-Csharp-for-Beginners) *codeproject.com* useful, ignoring hardware pin handling
  - mostly about UI;&nbsp; also uses `Invoke()`.
- [Improving the Performance of Serial Ports Using C#](https://www.codeproject.com/Articles/110670/Improving-the-Performance-of-Serial-Ports-Using-C) *codeproject.com*a
  - handshaking protocol;&nbsp; multiple threads.
- [Arduino, C#, and Serial Interface](https://www.codeproject.com/Articles/473828/Arduino-Csharp-and-Serial-Interface) *codeproject.com* 
  - Uses, but does not implement `NewWeatherDataReceived` event handler.
- [Top 5 SerialPort Tips](https://learn.microsoft.com/en-us/archive/blogs/bclteam/top-5-serialport-tips-kim-hamilton)
  - Inapplicable to `ReadExisting`;&nbsp;  potential dealock on `Close()`.
- [SerialPort Encoding](https://learn.microsoft.com/en-us/archive/blogs/bclteam/serialport-encoding-ryan-byington)
  - Instead, use bytes...
- [learn SerialPort Class](https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport?view=dotnet-plat-ext-7.0)
  -  uses threads and no event handlers
- [*instructables* Serial Port Programming With .NET](https://www.instructables.com/Serial-Port-Programming-With-NET/)
  - *very* simplistic, no data handling;&nbsp; does not use passed object in `SerialDataReceivedEventHandler`,
- [Communicate with Serial Port in C#](https://www.c-sharpcorner.com/UploadFile/eclipsed4utoo/communicating-with-serial-port-in-C-Sharp/) *c-sharp corner*
  - Introduces delegate for cross-thread data transfer and a read thread, which `SerialDataReceivedEventHandler` would make redundant.
- [**Signed com0com** Null-modem emulator](https://pete.akeo.ie/2011/07/com0com-signed-drivers.html) - Link for [Installing;&nbsp; FAQs](https://raw.githubusercontent.com/paulakg4/com0com/master/ReadMe)
