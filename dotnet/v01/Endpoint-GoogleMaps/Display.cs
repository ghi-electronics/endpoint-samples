using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Devices.Display;
using System;
using System.Collections.Generic;
using System.Device.Gpio.Drivers;
using System.Device.Gpio;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace EndpointGoogleMap
{
    internal static class Display
    {
        public static DisplayController Screen;
        public static int ScreenWidth => 800;
        public static int ScreenHeight => 480;
        public static void Initialize () {
            //Initialize Display
            var backlightPort = EPM815.Gpio.Pin.PD14 / 16;
            var backlightPin = EPM815.Gpio.Pin.PD14 % 16;
            var backlightDriver = new LibGpiodDriver((int)backlightPort);
            var backlightController = new GpioController(PinNumberingScheme.Logical, backlightDriver);
            backlightController.OpenPin(backlightPin);
            backlightController.SetPinMode(backlightPin, PinMode.Output);
            backlightController.Write(backlightPin, PinValue.High);

            var configuration = new FBDisplay.Configuration()
            {
                Clock = 24750,
                Width = 800,
                Hsync_start = 854,
                Hsync_end = 856,
                Htotal = 900,
                Height = 480,
                Vsync_start = 529,
                Vsync_end = 531,
                Vtotal = 533,
                Bus_flags = 63
            };

            var fbDisplay = new FBDisplay(configuration);
            Screen = new DisplayController(fbDisplay);
        }
    }
}
