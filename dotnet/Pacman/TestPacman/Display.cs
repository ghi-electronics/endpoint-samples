#define BBB
using GHIElectronics.Endpoint.Core;
using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Device.Spi;

using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static GHIElectronics.Endpoint.Core.EPM815;
using static GHIElectronics.Endpoint.Drivers.HiLetgo.ILI9341.ILI9341;

namespace TestPacman
{
    public class Display
    {
#if ENDPOINT
        int csPin = EPM815.Gpio.Pin.PA14;
        int dataControlPin = EPM815.Gpio.Pin.PF14;
        int resetPin = EPM815.Gpio.Pin.PF4;
#endif
#if RPI
        int csPin = 2;
        int resetPin = 3;
        int dataControlPin = 4;
#endif

#if BBB
        int csPin = 66;
        int resetPin = 69;
        int dataControlPin = 45;

        //ls -l /dev/spidev*
        //sudo groupadd spi
        //sudo adduser debian spi
        //sudo chgrp spi /dev/spidev0.0
        //sudo chmod 660 /dev/spidev0.0
        // config-pin P9.18 spi
        // config-pin P9.22 spi_sclk

#endif

        ILI9341Controller displayController;
        public Display()
        {

#if ENDPOINT
            EPM815.Spi.Initialize(EPM815.Spi.Spi1, 8192);
            var setting = ILI9341Controller.GetConnectionSettings(EPM815.Spi.Spi1);
#endif
#if RPI
            var setting = ILI9341Controller.GetConnectionSettings(0);
#endif
#if BBB
            var setting = ILI9341Controller.GetConnectionSettings(0);
#endif


            displayController = new ILI9341Controller(SpiDevice.Create(setting), csPin, dataControlPin, resetPin);

            displayController.Enable();
        }

        public void Flush(byte[] data, int offset, int length)
        {
            displayController?.DrawBuffer(data, offset, length   );
        }

        


        
    }

}
