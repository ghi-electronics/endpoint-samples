using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Devices.Display;
using GHIElectronics.Endpoint.Drivers.FocalTech.FT5xx6;
using GHIElectronics.Endpoint.UI.Media;
using GHIElectronics.Endpoint.UI;
using System.Device.Gpio.Drivers;
using System.Device.Gpio;
using GHIElectronics.Endpoint.Drawing;

namespace CarWashUI
{
    internal class Program : Application
    {
        public static Program MainApp;

        public static Window WpfWindow { get; set; }
        public static SelectServiceWindow SelectServicePage { get; set; }
        public static PaymentWindow PaymentePage { get; set; }
        public static LoadingPage LoadingPage { get; set; }
        public static CarWashPage CarWashPage { get; set; }
        public static EndPage EndPage { get; set; }
        public Program(int width, int height) : base(width, height)
        {
        }

        static void Main(string[] args)
        {
            InitDisplay();
            Graphics.OnFlushEvent += Graphics_OnFlushEvent;

            MainApp = new Program(displayController.Configuration.Width, displayController.Configuration.Height);

            InitializeTouch();

            SelectServicePage = new SelectServiceWindow();
            PaymentePage = new PaymentWindow();

            LoadingPage = new LoadingPage();
            CarWashPage = new CarWashPage();
            EndPage = new EndPage();

            WpfWindow = Program.CreateWindow(displayController.Configuration.Width, displayController.Configuration.Height);
            WpfWindow.Child = SelectServicePage.Elements;
            WpfWindow.Visibility = Visibility.Visible;


            MainApp.Run(WpfWindow);
        }

        private static void Graphics_OnFlushEvent(Graphics sender, byte[] data, int x, int y, int width, int height, int originalWidth)
        {
            displayController.Flush(data, 0, data.Length, x, y, width, height, originalWidth);
        }

        static DisplayController displayController;
        static void InitDisplay()
        {
            var backlightPort = EPM815.Gpio.Pin.PD14 / 16;
            var backlightPin = EPM815.Gpio.Pin.PD14 % 16;

            var gpioDriver = new LibGpiodDriver(backlightPort);
            var gpioController = new GpioController(PinNumberingScheme.Logical, gpioDriver);

            gpioController.OpenPin(backlightPin, PinMode.Output);
            gpioController.Write(backlightPin, PinValue.High); // low is on


            var configuration = new FBDisplay.Configuration()
            {
                Clock = 10000,
                Width = 480,
                Hsync_start = 480 + 2,
                Hsync_end = 480 + 2 + 41,
                Htotal = 480 + 2 + 41 + 2,
                Height = 272,
                Vsync_start = 272 + 2,
                Vsync_end = 272 + 2 + 10,
                Vtotal = 272 + 2 + 10 + 2,

            };

            var setting = $"{configuration.Clock},";

            setting += $"{configuration.Width},{configuration.Hsync_start},{configuration.Hsync_end},{configuration.Htotal},";
            setting += $"{configuration.Height},{configuration.Vsync_start},{configuration.Vsync_end},{configuration.Vtotal},";
            setting += $"{configuration.Num_modes},{configuration.Dpi_width},{configuration.Dpi_height},{configuration.Bus_flags},{configuration.Bus_format},{configuration.Connector_type},{configuration.Bpc}";

            Console.WriteLine(setting);

            var fbDisplay = new FBDisplay(configuration);

            displayController = new DisplayController(fbDisplay);
        }

        public static void InitializeTouch()
        {
            var resetPin = EPM815.Gpio.Pin.PF2 % 16;
            var resetPort = EPM815.Gpio.Pin.PF2 / 16;

            var gpioController = new GpioController(PinNumberingScheme.Logical, new LibGpiodDriver(resetPort));
            gpioController.OpenPin(resetPin);
            gpioController.Write(resetPin, PinValue.Low);

            Thread.Sleep(100);

            gpioController.Write(resetPin, PinValue.High);



            // On dev
            //EPM815.I2c.Initialize(EPM815.I2c.I2c6);
            //var touch = new FT5xx6Controller(EPM815.I2c.I2c6, EPM815.Gpio.Pin.PF12);

            // On Domino
            EPM815.I2c.Initialize(EPM815.I2c.I2c5);
            var touch = new FT5xx6Controller(EPM815.I2c.I2c5, EPM815.Gpio.Pin.PB11);

            touch.TouchDown += (a, b) =>
            {
                //Console.WriteLine("Touch down " + b.X + ", " + b.Y);
                Program.MainApp.InputProvider.RaiseTouch(b.X, b.Y, GHIElectronics.Endpoint.UI.Input.TouchMessages.Down, System.DateTime.Now);
            };

            touch.TouchUp += (a, b) =>
            {
                //Console.WriteLine("Touch up " + b.X + ", " + b.Y);

                Program.MainApp.InputProvider.RaiseTouch(b.X, b.Y, GHIElectronics.Endpoint.UI.Input.TouchMessages.Up, System.DateTime.Now);
            };

            //Thread.Sleep(-1);
        }

        private static Window CreateWindow(int width, int height)
        {
            var window = new Window
            {
                Height = height,
                Width = width
            };
            window.Background = new LinearGradientBrush(Colors.Blue, Colors.Teal, 0, 0,
              window.Width, window.Height);

            //window.Background = null;

            return window;
        }
    }
}
