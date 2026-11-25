using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Devices.Display;
using GHIElectronics.Endpoint.Drivers.FocalTech.FT5xx6;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EndpointGoogleMap
{
    public static class FMRadio
    {
        static DisplayController displayController;

        //Chicago Radio Stations
        static double preset1 = 91.5;
        static double preset2 = 93.9;
        static double preset3 = 94.7;
        static double preset4 = 95.5;
        static double preset5 = 101.1;
        static double preset6 = 102.7;
        static double preset7 = 103.5;
        static double preset8 = 107.5;

        ////Detroit Radio Stations
        //static double preset1 = 89.3;
        //static double preset2 = 94.7;
        //static double preset3 = 96.3;
        //static double preset4 = 97.1;
        //static double preset5 = 97.9;
        //static double preset6 = 101.1;
        //static double preset7 = 101.9;
        //static double preset8 = 106.7;

        //Initialize FM Click module - MikroBus2
        static int reset = EPM815.Gpio.Pin.PF7;
        static int cs = EPM815.Gpio.Pin.PF13;
        static int i2c = EPM815.I2c.I2c6;

        static double currentStation = 100;
        static int volume = 125;

        public static bool IsEnabled = false;

        static SKBitmap bitmapBackgroundImage;
    
        static SKBitmap bitmap;

        static SKCanvas screen;
        public static void Initialize(DisplayController display)
        {
            displayController = display;

            //var radio = new FM_Click(reset, cs)
            //{
            //    Channel = currentStation,
            //    Volume = volume,
            //};


            var img = Resources.fmradiobg;
            var info = new SKImageInfo(display.Configuration.Width, display.Configuration.Height); // width and height of rect
            bitmapBackgroundImage = SKBitmap.Decode(img, info);

            bitmap = new SKBitmap(display.Configuration.Width, display.Configuration.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            

            screen = new SKCanvas(bitmap);
        }

        public static void EnableRadio()
        {
            if (IsEnabled)
                return;

            IsEnabled = true;
            Touch.TouchUpEventHandler += TouchUpEvent;
        }

        public static void DisableRadio()
        {
            if (!IsEnabled)
                return;

            Touch.TouchUpEventHandler -= TouchUpEvent;
            IsEnabled = false;
            
        }

        public static void DrawRadio()
        {
            while (IsEnabled)
            {

                //Create Black Screen 
                screen.DrawColor(SKColors.Black);
                screen.Clear(SKColors.Black);

                // Draw background from resource
                screen.DrawBitmap(bitmapBackgroundImage, 0, 0);

                // Font from Resources
                byte[] fontfile = Resources.LCD;
                Stream stream = new MemoryStream(fontfile);

                using (SKPaint currentText = new SKPaint())
                using (SKPaint presetText = new SKPaint())
                using (SKTypeface tf = SKTypeface.FromStream(stream))
                {
                    // Current Station Text Properties
                    currentText.Color = SKColors.Red;
                    currentText.IsAntialias = true;
                    currentText.StrokeWidth = 2;
                    currentText.Style = SKPaintStyle.Fill;

                    // Station Preset Text Properties
                    presetText.Color = SKColors.White;
                    presetText.IsAntialias = true;
                    presetText.StrokeWidth = 2;
                    presetText.Style = SKPaintStyle.Fill;

                    // Current Station Font
                    SKFont currentFont = new SKFont();
                    currentFont.Size = 153;
                    currentFont.Typeface = tf;
                    SKTextBlob textBlob = SKTextBlob.Create(currentStation.ToString("F1"), currentFont);

                    // Draw Current Station
                    if (currentStation >= 100)
                        screen.DrawText(textBlob, 250, 264, currentText);
                    else
                        screen.DrawText(textBlob, 280, 264, currentText);

                    // Preset Buttons Font
                    SKFont presetButtonsFont = new SKFont();
                    presetButtonsFont.Size = 29;
                    presetButtonsFont.Typeface = tf;

                    // Draw Date and Time 
                    SKTextBlob dateTime = SKTextBlob.Create(DateTime.Now.ToString(), presetButtonsFont);
                    screen.DrawText(dateTime, 520, 64, presetText);

                    // Preset 1
                    int x1 = 55;
                    int x2 = 60;

                    int x_off = 80;

                    // preset1 is off
                    SKTextBlob presetButton1 = SKTextBlob.Create("OFF", presetButtonsFont);
                    if (preset1 >= 100)
                        screen.DrawText(presetButton1, x1, 410, presetText); 
                    else
                        screen.DrawText(presetButton1, x2, 410, presetText);



                    // Preset 2
                    x1 += x_off;
                    x2 += x_off;
                    SKTextBlob presetButton2 = SKTextBlob.Create(preset2.ToString("F1"), presetButtonsFont);
                    if (preset2 >= 100)
                        screen.DrawText(presetButton2, x1, 410, presetText);
                    else
                        screen.DrawText(presetButton2, x2, 410, presetText);


                    // Preset 3
                    x1 += x_off;
                    x2 += x_off;
                    SKTextBlob presetButton3 = SKTextBlob.Create(preset3.ToString("F1"), presetButtonsFont);
                    if (preset3 >= 100)
                        screen.DrawText(presetButton3, x1, 410, presetText);
                    else
                        screen.DrawText(presetButton3, x2, 410, presetText);

                    // Preset 4
                    x1 += x_off;
                    x2 += x_off;
                    SKTextBlob presetButton4 = SKTextBlob.Create(preset4.ToString("F1"), presetButtonsFont);
                    if (preset4 >= 100)
                        screen.DrawText(presetButton4, x1, 410, presetText);
                    else
                        screen.DrawText(presetButton4, x2, 410, presetText);

                    // Preset 5
                    x1 += x_off;
                    x2 += x_off;
                    SKTextBlob presetButton5 = SKTextBlob.Create(preset5.ToString("F1"), presetButtonsFont);
                    if (preset5 >= 100)
                        screen.DrawText(presetButton5, x1, 410, presetText);
                    else
                        screen.DrawText(presetButton5, x2, 410, presetText);

                    // Preset 6
                    x1 += x_off;
                    x2 += x_off;
                    SKTextBlob presetButton6 = SKTextBlob.Create(preset6.ToString("F1"), presetButtonsFont);
                    if (preset6 >= 100)
                        screen.DrawText(presetButton6, x1, 410, presetText);
                    else
                        screen.DrawText(presetButton6, x2, 410, presetText);

                    // Preset 7
                    x1 += x_off;
                    x2 += x_off;
                    SKTextBlob presetButton7 = SKTextBlob.Create(preset7.ToString("F1"), presetButtonsFont);
                    if (preset7 >= 100)
                        screen.DrawText(presetButton7, x1, 410, presetText);
                    else
                        screen.DrawText(presetButton7, x2, 410, presetText);

                    // Preset 8
                    x1 += x_off;
                    x2 += x_off;
                    SKTextBlob presetButton8 = SKTextBlob.Create(preset8.ToString("F1"), presetButtonsFont);
                    if (preset8 >= 100)
                        screen.DrawText(presetButton8, x1, 410, presetText);
                    else
                        screen.DrawText(presetButton8, x2, 410, presetText);
                }

                SKPaint volumeLineGreen = new SKPaint();
                volumeLineGreen.Color = SKColors.Green;
                volumeLineGreen.IsAntialias = true;
                volumeLineGreen.StrokeWidth = 25;
                volumeLineGreen.Style = SKPaintStyle.Fill;

                SKPaint volumeLineRed = new SKPaint();
                volumeLineRed.Color = SKColors.Red;
                volumeLineRed.IsAntialias = true;
                volumeLineRed.StrokeWidth = 25;
                volumeLineRed.Style = SKPaintStyle.Fill;

                for (int y = 140; y <= 380; y += 34)
                {
                    screen.DrawLine(700, y, 755, y, volumeLineGreen);
                }

                switch (volume)
                {
                    case 25:
                        screen.DrawLine(700, 378, 755, 378, volumeLineRed);
                        screen.DrawLine(700, 344, 755, 344, volumeLineRed);
                        break;
                    case 75:
                        screen.DrawLine(700, 378, 755, 378, volumeLineRed);
                        screen.DrawLine(700, 344, 755, 344, volumeLineRed);
                        screen.DrawLine(700, 310, 755, 310, volumeLineRed);
                        screen.DrawLine(700, 276, 755, 276, volumeLineRed);
                        break;
                    case 125:
                        screen.DrawLine(700, 378, 755, 378, volumeLineRed);
                        screen.DrawLine(700, 344, 755, 344, volumeLineRed);
                        screen.DrawLine(700, 310, 755, 310, volumeLineRed);
                        screen.DrawLine(700, 276, 755, 276, volumeLineRed);
                        screen.DrawLine(700, 242, 755, 242, volumeLineRed);
                        screen.DrawLine(700, 208, 755, 208, volumeLineRed);
                        break;
                    case 175:
                        screen.DrawLine(700, 378, 755, 378, volumeLineRed);
                        screen.DrawLine(700, 344, 755, 344, volumeLineRed);
                        screen.DrawLine(700, 310, 755, 310, volumeLineRed);
                        screen.DrawLine(700, 276, 755, 276, volumeLineRed);
                        screen.DrawLine(700, 242, 755, 242, volumeLineRed);
                        screen.DrawLine(700, 208, 755, 208, volumeLineRed);
                        screen.DrawLine(700, 174, 755, 174, volumeLineRed);
                        screen.DrawLine(700, 140, 755, 140, volumeLineRed);
                        break;

                }

                //Flush to screen
               

                var data = bitmap.Copy(SKColorType.Rgb565).Bytes;
                displayController.Flush(data);
                Thread.Sleep(1);

                if (currentStation < 88.0) currentStation = 88.0;
                if (currentStation > 108.0) currentStation = 108;

                Thread.Sleep(100);

            }
        }

        static void TouchUpEvent(int x, int y)
        {
            if (!IsEnabled)
                return;

            if (x >= 150 && x <= 230 && y >= 170 && y <= 260)
            {
                currentStation = currentStation - 0.1;
                FM_Click radio = new FM_Click(reset, cs, i2c);

                radio.Channel = currentStation;
                radio.Volume = volume;

                Console.WriteLine("Radio Station " + currentStation.ToString());
                return;
            }



            else if (x >= 540 && x <= 640 && y >= 170 && y <= 260)
            {
                currentStation = currentStation + 0.1;
                FM_Click radio = new FM_Click(reset, cs, i2c);

                radio.Channel = currentStation;
                radio.Volume = volume;
                Console.WriteLine("Radio Station " + currentStation.ToString());
                return;
            }

            //Touch Preset 1 => OFF
            else if(x >= 45 && x <= 110 && y >= 375 && y <= 435)
            {
                FM_Click radio = new FM_Click(reset, cs, i2c);
                currentStation = preset2;
                //radio.Channel = currentStation;
                radio.Volume = 0;
                Console.WriteLine("Radio Station OFF: " + currentStation.ToString());
                return;
            }

            //Touch Preset 2
            else if (x >= 125 && x <= 190 && y >= 375 && y <= 435)
            {
                FM_Click radio = new FM_Click(reset, cs, i2c);
                currentStation = preset2;
                radio.Channel = currentStation;
                radio.Volume = volume;
                Console.WriteLine("Radio Station " + currentStation.ToString());
                return;
            }

            //Touch Preset 3
            else if (x >= 205 && x <= 270 && y >= 375 && y <= 435)
            {
                FM_Click radio = new FM_Click(reset, cs, i2c);
                currentStation = preset3;
                radio.Channel = currentStation;
                radio.Volume = volume;
                Console.WriteLine("Radio Station " + currentStation.ToString());
                return;
            }

            //Touch Preset 4
            else if (x >= 285 && x <= 350 && y >= 375 && y <= 435)
            {
                FM_Click radio = new FM_Click(reset, cs, i2c);
                currentStation = preset4;
                radio.Channel = currentStation;
                radio.Volume = volume;
                Console.WriteLine("Radio Station " + currentStation.ToString());
                return;
            }

            //Touch Preset 5
            else if (x >= 365 && x <= 430 && y >= 375 && y <= 435)
            {
                FM_Click radio = new FM_Click(reset, cs, i2c);
                currentStation = preset5;
                radio.Channel = currentStation;
                radio.Volume = volume;
                Console.WriteLine("Radio Station " + currentStation.ToString());
                return;
            }

            //Touch Preset 6
            else if (x >= 445 && x <= 510 && y >= 375 && y <= 435)
            {
                FM_Click radio = new FM_Click(reset, cs, i2c);
                currentStation = preset6;
                radio.Channel = currentStation;
                radio.Volume = volume;
                Console.WriteLine("Radio Station " + currentStation.ToString());
                return;
            }

            //Touch Preset 7
            else if (x >= 525 && x <= 590 && y >= 375 && y <= 435)
            {
                FM_Click radio = new FM_Click(reset, cs, i2c);
                currentStation = preset7;
                radio.Channel = currentStation;
                radio.Volume = volume;
                Console.WriteLine("Radio Station " + currentStation.ToString());
                return;
            }

            //Touch Preset 8
            else if (x >= 605 && x <= 670 && y >= 375 && y <= 435)
            {
                FM_Click radio = new FM_Click(reset, cs, i2c);
                currentStation = preset8;
                radio.Channel = currentStation;
                radio.Volume = volume;
                Console.WriteLine("Radio Station " + currentStation.ToString());
                return;
            }

            //Volume Up
            else if (x >= 700 && x <= 755 && y >= 75 && y <= 120)
            {
                FM_Click radio = new FM_Click(reset, cs, i2c);
                volume = volume + 50;
                if (volume > 175)
                    volume = 175;

                radio.Channel = currentStation;
                radio.Volume = volume;
                
                Console.WriteLine("Volume " + volume.ToString());
                return;
            }

            //Volume Down
            else if (x >= 700 && x <= 755 && y >= 400 && y <= 440)
            {
                FM_Click radio = new FM_Click(reset, cs, i2c);
                volume = volume - 50;
                if (volume < 0)
                    volume = 0;
                radio.Channel = currentStation;
                radio.Volume = volume;
                
                Console.WriteLine("Volume " + volume.ToString());

                //Added to fix volume bug in module
                if (volume <= 0)
                    volume = -25;

                return;
            }
            else
            {

                DisableRadio();
                MainMenu.EnableMainMenu();

            }

        }


    }
}
