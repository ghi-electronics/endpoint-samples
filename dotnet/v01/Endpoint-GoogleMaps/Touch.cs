using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Drivers.FocalTech.FT5xx6;
using System;
using System.Collections.Generic;
using System.Device.Gpio.Drivers;
using System.Device.Gpio;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EndpointGoogleMap
{
    public static class Touch
    {
        public static FT5xx6Controller TouchController;

        public delegate void TouchEvent(int x, int y);

        public static event TouchEvent TouchUpEventHandler;
        public static void Initialize() {
            //Initialize Touch
            var touchResetPin = EPM815.Gpio.Pin.PF2 % 16;
            var touchResetPort = EPM815.Gpio.Pin.PF2 / 16;
            var touchController = new GpioController(PinNumberingScheme.Logical, new LibGpiodDriver(touchResetPort));
            touchController.OpenPin(touchResetPin);
            touchController.Write(touchResetPin, PinValue.Low);
            Thread.Sleep(100);
            touchController.Write(touchResetPin, PinValue.High);
            EPM815.I2c.Initialize(EPM815.I2c.I2c6);
            
            TouchController = new FT5xx6Controller(EPM815.I2c.I2c6, EPM815.Gpio.Pin.PF12);

            TouchController.TouchUp += TouchController_TouchUp;


        }

        private static void TouchController_TouchUp(FT5xx6Controller sender, FT5xx6Controller.TouchEventArgs e)
        {
            TouchUpEventHandler?.Invoke(e.X, e.Y);
        }
    }
}
