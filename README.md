
---
Fake8:&nbsp; proposed SimHub 8-bit serial plugin
---

As noted in [Arduino for STM32 Black 'n Blue Pills, ESP32-S[2,3] ](https://blekenbleu.github.io/Arduino/),  
 [SimHub's **Custom Serial devices** plugin](https://github.com/SHWotever/SimHub/wiki/Custom-serial-devices) has limitations
- SimHub plugin Javascript is relatively inefficient, hard to debug and maintain.
- Plugin can log but not process received serial port messages from e.g. from Arduino.
- Serial data is limited to 7 bits per character.

This `Fake8` SimHub plugin connects to (Arduino) device serial ports,
using 8 bit characters and executing C#,  
working with SimHub's Custom Serial devices plugin by properties.
and a **signed** [virtual com0com Null-modem](https://pete.akeo.ie/2011/07/com0com-signed-drivers.html).  
This leverages the **SimHub Custom Serial devices** plugin user interface,  
while much of the heavy lifting gets done by `Fake8`.  
Sadly, `Custom Serial devices` user interface control properties are local  
and cannot be accessed by another plugin, such as `Fake8`.  
Consequently, `Custom Serial devices` must send those control settings via `Fake8` Serial port.  
Incoming `Fake8` serial data will generally combine Arduino and `Fake8` strings.

`Fake8` to Arduino will approximate MIDI protocol, with:  
- only first message 8-bit characters having msb ==1
- 7 lsb of first message character are a command
- second character is 7-bit data
- for some commands, that second character 7-bit data is count for appended 7-bit character array of values.  
  One string command to echoes that string.  
  One non-string command echoes that second character.
  Another non-string command resets the Arduino run-time sketch.
- for commands with 1 == second-most significant bit of first message character,  
  3 lsb index Arduino device application-specific settings, such as
  - setting **PWM** pin parameters, e.g.:&nbsp; frequency, % range, predistortion, PWM pin number, clock number

This supports 80 commands:
   - 16 for application-specific settings with 3-bit indexing.
   - 64 for string and other purposes.

### potentially useful C# serial port references
- [Close Serial COM Port safely in C#](https://www.codeproject.com/Questions/281222/Close-Serial-COM-Port-safely-in-Csharp) *codeproject.com*
- [Top 5 SerialPort Tips](https://learn.microsoft.com/en-us/archive/blogs/bclteam/top-5-serialport-tips-kim-hamilton)
- [SerialPort Encoding](https://learn.microsoft.com/en-us/archive/blogs/bclteam/serialport-encoding-ryan-byington)
- [learn SerialPort Class](https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport?view=dotnet-plat-ext-7.0)
- [*instructables* Serial Port Programming With .NET](https://www.instructables.com/Serial-Port-Programming-With-NET/)
- [Communicate with Serial Port in C#](https://www.c-sharpcorner.com/UploadFile/eclipsed4utoo/communicating-with-serial-port-in-C-Sharp/) *c-sharp corner*
- [**Signed com0com** Null-modem emulator](https://pete.akeo.ie/2011/07/com0com-signed-drivers.html) - [How to use and configure](https://com0com.sourceforge.net/doc/UsingCom0com.pdf)
