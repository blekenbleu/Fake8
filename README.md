
---
Fake8:&nbsp; proposed SimHub 8-bit serial plugin
---

As noted in [Arduino for STM32 Black 'n Blue Pills, ESP32-S[2,3] ](https://blekenbleu.github.io/Arduino/),  
 [SimHub's **Custom Serial devices** plugin](https://github.com/SHWotever/SimHub/wiki/Custom-serial-devices) has limitations
- SimHub plugin Javascript is relatively inefficient, hard to debug and maintain.
- Plugin can log but not process received serial port messages from e.g. from Arduino.

This `Fake8` SimHub plugin would connect to real device serial port,
using 8 bit characters and executing C#,  
while interacting with SimHub's Custom Serial devices plugin by properties
and a [virtual com0com Null-modem](https://com0com.sourceforge.net/).  
This leverages the **SimHub Custom Serial devices** plugin user interface,  
while much of the heavy lifting gets done by `Fake8`.

`Fake8` would approximate MIDI protocol, with:  
- only first message 8-bit characters having msb ==1
- 7 lsb of first message character are a command
- second character is 7-bit data
- for some commands, that second character 7-bit data is count for appended 7-bit character array of values.  
  One string application is to echo that string.  
  One non-string application is to echo that second character.
  Another non-string command resets the Arduino run-time sketch.
- for commands with 1 == second-most significant bit of first message character,  
  3 lsb index Arduino device application-specific settings, such as
  - setting **PWM** pin parameters, e.g.:&nbsp; frequency, % range, predistortion, PWM pin number, clock number

This supports 80 commands:
   - 16 for application-specific settings with 3-bit indexing.
   - 64 for string and other purposes.
