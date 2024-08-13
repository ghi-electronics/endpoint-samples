using System;
using System.Collections;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Device.Spi;
using System.Text;
using System.Threading;



namespace GHIElectronics.Endpoint.Drivers.HiLetgo.ILI9341 {
    public class ILI9341 {
        public enum ILI9341CommandId : byte {
            SWRESET = 0x01,
            SLPOUT = 0x11,
            INVOFF = 0x20,
            INVON = 0x21,
            GAMMASET = 0x26,
            DISPOFF = 0x28,
            DISPON = 0x29,
            CASET = 0x2A,
            PASET = 0x2B,
            RAMWR = 0x2C,
            MADCTL = 0x36,
            PIXFMT = 0x3A,
            FRMCTR1 = 0xB1,
            DFUNCTR = 0xB6,
            EMSET = 0xB7,
            MADCTL_MY = 0x80,
            MADCTL_MX = 0x40,
            MADCTL_MV = 0x20,
            MADCTL_BGR = 0x08,
            MADCTL_RGB = 0x00
        }

        public class ILI9341Controller {
            private readonly byte[] buffer1 = new byte[1];

            private readonly SpiDevice spi;
            private readonly int dePin;
            private readonly int resetPin;
            private readonly int csPin;
            private GpioController resetGpioController;
            private GpioController controlGpioController;
            private GpioController csGpioController;

            private bool rowColumnSwapped;

            const int SPI_BLOCK_SIZE = 4096;

            public int Width {
                get; private set;
            }
            public int Height {
                get; private set;
            }

            private int bpp = 16;

            public int MaxWidth => this.rowColumnSwapped ? 320 : 240;
            public int MaxHeight => this.rowColumnSwapped ? 240 : 320;

            public static SpiConnectionSettings GetConnectionSettings(int spi) => new SpiConnectionSettings(spi)
            {
                Mode = SpiMode.Mode0,
                ClockFrequency = 12_000_000,               
                ChipSelectLine = 0
            };



            public ILI9341Controller(SpiDevice spi, int cs, int control, int reset) {
                this.spi = spi;

                this.dePin = control;
                this.controlGpioController = new GpioController(PinNumberingScheme.Logical, new LibGpiodDriver(this.dePin / 16));

                this.controlGpioController.OpenPin(this.dePin % 16);
                this.controlGpioController.SetPinMode(this.dePin % 16, PinMode.Output);
                this.controlGpioController.Write(this.dePin % 16, PinValue.High);


                this.resetPin = reset;
                this.resetGpioController = new GpioController(PinNumberingScheme.Logical, new LibGpiodDriver(this.resetPin / 16));
                this.resetGpioController.OpenPin(this.resetPin % 16);
                this.resetGpioController.SetPinMode(this.resetPin % 16, PinMode.Output);
                this.resetGpioController.Write(this.resetPin % 16, PinValue.High);

                this.csPin = cs;
                this.csGpioController = new GpioController(PinNumberingScheme.Logical, new LibGpiodDriver(this.csPin / 16));
                this.csGpioController.OpenPin(this.csPin % 16);
                this.csGpioController.SetPinMode(this.csPin % 16, PinMode.Output);
                this.csGpioController.Write(this.csPin % 16, PinValue.High);


                this.Reset();
                this.Initialize();
                this.SetDataAccessControl(!false, !false, !false, true);
                this.SetDrawWindow(0, 0, this.MaxWidth-1, this.MaxHeight-1);

                this.Enable();
            }

            private void Reset() {
                this.resetGpioController?.Write(this.resetPin % 16, PinValue.Low);
                Thread.Sleep(50);

                this.resetGpioController?.Write(this.resetPin % 16, PinValue.High);
                Thread.Sleep(200);
            }

            private void Initialize() {
                this.SendCommand(ILI9341CommandId.SWRESET);
                Thread.Sleep(10);

                this.SendCommand(ILI9341CommandId.DISPOFF);

                this.SendCommand(ILI9341CommandId.MADCTL);

                this.SendData(0x08 | 0x40);

                this.SendCommand(ILI9341CommandId.PIXFMT);
                this.SendData(0x55);

                this.SendCommand(ILI9341CommandId.FRMCTR1);
                this.SendData(0x00);
                this.SendData(0x1B);

                this.SendCommand(ILI9341CommandId.GAMMASET);
                this.SendData(0x01);

                this.SendCommand(ILI9341CommandId.CASET);
                this.SendData(0x00);
                this.SendData(0x00);
                this.SendData(0x00);
                this.SendData(0xEF);

                this.SendCommand(ILI9341CommandId.PASET);
                this.SendData(0x00);
                this.SendData(0x00);
                this.SendData(0x01);
                this.SendData(0x3F);

                this.SendCommand(ILI9341CommandId.EMSET);
                this.SendData(0x07);

                this.SendCommand(ILI9341CommandId.DFUNCTR);
                this.SendData(0x0A);
                this.SendData(0x82);
                this.SendData(0x27);
                this.SendData(0x00);

                this.SendCommand(ILI9341CommandId.SLPOUT);
                Thread.Sleep(120);

                this.SendCommand(ILI9341CommandId.DISPON);
                Thread.Sleep(100);
            }

            public void Dispose() {
                this.spi.Dispose();
                this.controlGpioController.Dispose();
                this.resetGpioController?.Dispose();
            }

            public void Enable() => this.SendCommand(ILI9341CommandId.DISPON);
            public void Disable() => this.SendCommand(ILI9341CommandId.DISPOFF);

            private void SendCommand(ILI9341CommandId command) {
                this.buffer1[0] = (byte)command;
                this.controlGpioController.Write(this.dePin % 16, PinValue.Low);

                this.csGpioController.Write(this.csPin % 16, PinValue.Low);
                this.spi.Write(this.buffer1);
                this.csGpioController.Write(this.csPin % 16, PinValue.High);
            }

            private void SendData(byte data) {
                this.buffer1[0] = data;
                this.controlGpioController.Write(this.dePin % 16, PinValue.High);

                this.csGpioController.Write(this.csPin % 16, PinValue.Low);
                this.spi.Write(this.buffer1);
                this.csGpioController.Write(this.csPin % 16, PinValue.High);
            }

            private void SendData(byte[] data) {
                this.controlGpioController.Write(this.dePin % 16, PinValue.High);

                this.csGpioController.Write(this.csPin % 16, PinValue.Low);
                this.spi.Write(data);
                this.csGpioController.Write(this.csPin % 16, PinValue.High);
            }


            public void SetDrawWindow(int x, int y, int width, int height) {
                var x_end = x + width;
                var y_end = y + height;

                this.SendCommand(ILI9341CommandId.CASET);
                this.SendData((byte)(x >> 8));
                this.SendData((byte)x);
                this.SendData((byte)(x_end >> 8));
                this.SendData((byte)x_end);

                this.SendCommand(ILI9341CommandId.PASET);
                this.SendData((byte)(y >> 8));
                this.SendData((byte)y);
                this.SendData((byte)(y_end >> 8));
                this.SendData((byte)y_end);

                this.SendCommand(ILI9341CommandId.RAMWR);

                this.Width = width;
                this.Height = height;
            }

            public void SetDataAccessControl(bool swapRowColumn, bool invertRow, bool invertColumn, bool useBgrPanel) {
                var val = default(byte);

                if (useBgrPanel) val |= 0b0000_1000;
                if (swapRowColumn) val |= 0b0010_0000;
                if (invertColumn) val |= 0b0100_0000;
                if (invertRow) val |= 0b1000_0000;

                this.SendCommand(ILI9341CommandId.MADCTL);
                this.SendData(val);

                this.rowColumnSwapped = swapRowColumn;
            }

            private void SendDrawCommand() {
                this.SendCommand(ILI9341CommandId.RAMWR);

                this.controlGpioController.Write(this.dePin % 16, PinValue.High);
            }

            public void DrawBuffer(byte[] buffer, int offset, int length) {
                this.SendDrawCommand();



                SwapEndianness(buffer, offset, length);

                var block = length / SPI_BLOCK_SIZE;
                var remain = length % SPI_BLOCK_SIZE;
                var index = offset;

                this.csGpioController.Write(this.csPin % 16, PinValue.Low);

                while (block > 0)
                {
                    var data = new byte[SPI_BLOCK_SIZE];

                    Array.Copy(buffer, index, data, 0, data.Length);
                    index += data.Length;
                    block--;

                    
                    this.spi.Write(data);

                }

                if (remain > 0)
                {
                    var data = new byte[remain];
                    Array.Copy(buffer, index, data, 0, data.Length);
                    index += data.Length;

                   
                    this.spi.Write(data);
                }
                
                this.csGpioController.Write(this.csPin % 16, PinValue.High);

               
            }

            

            static void SwapEndianness(byte[] data, int offset, int length)
            {
                for (int i = offset; i < length; i+=2)
                {
                    var tmp = data[i];
                    data[i] = data[i+1];
                    data[i+1] = tmp;
                }

            }
        }
    }
}
