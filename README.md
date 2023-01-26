# Clock Project
This project consists of two halves; a controller for an LED strip-based 7 segment display clock, and an app to interact with the controller via Bluetooth Low Energy.

## Controller
A LPC1114 microcontroller is used for this purpose, programmed in C.
- For time keeping a real-time clock (DS3231) is connected via I2C.
- Custom commands are received from a BLE module connected via UART.
- The addressable LED strip used (WS2812B LEDs) has a data transfer protocol with timing requirements that allow it to be emulated by a SPI data signal.
- Overall the program receives commands, reacts accordingly, and sends updated data to the LEDs when necessary.

## Mobile App
The mobile app is made with Xamarin.Forms.
It allows the user to discover nearby BLE devices and connect to a device with the required service and characteristic. The device selection persists between app launches.
When successfully connected, the user can:
- Change the colour of any or all parts of the clock.
- Change the brightness of the clock.
- Display the time as usual or manually control which segments should be lit.
- Sync the clock's time to their device's.
