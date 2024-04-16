using GHIElectronics.Endpoint.Devices.Display;
using GHIElectronics.Endpoint.Drivers.VirtualKeyboard;
using Iot.Device.PiJuiceDevice.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace EndpointGoogleMap
{
    public static class Weather
    {
        static WeatherInfo weatherInfo;

        static SKBitmap bitmapMapBackground;
        static SKBitmap bitmapMap;


        static DisplayController displayController;

        public static bool IsEnabled = false;

        public static SKCanvas canvas;
        static string CurrentLocation { get; set; } = "Chicago";

        static SKPaint paintWhite;
        static SKPaint paintWhiteFill;
        static SKPaint paintBlack;
        public static void Initialize(DisplayController display)
        {
            displayController = display;

            var img = Resources.weatherbg;
            var info = new SKImageInfo(displayController.Configuration.Width, displayController.Configuration.Height); // width and height of rect
            bitmapMapBackground = SKBitmap.Decode(img, info);

            bitmapMap = new SKBitmap(displayController.Configuration.Width, displayController.Configuration.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);



            paintWhite = new SKPaint() { Style = SKPaintStyle.Stroke, Color = SKColors.White };
            paintWhiteFill = new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.White };
            paintBlack = new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.Black };

            canvas = new SKCanvas(bitmapMap);


            weatherInfo = new WeatherInfo("your api key");

            keyboard = new VirtualKeyboard(display);

            keyboard.OnClose += Keyboard_OnClose;

        }

        private static void Keyboard_OnClose()
        {
            if (keyboard.Text != null && keyboard.Text.Length > 0)
            {
                counter = 0;

                CurrentLocation = keyboard.Text;    
            }
        }

        static int city_x = 10;
        static int city_y = 10;


        static int counter = 0;

        static bool IsValidLocation()
        {
            return CurrentLocation != null && CurrentLocation != string.Empty && CurrentLocation != "N/A" && CurrentLocation.Length > 0;
        }
        static void GetWeatherInfo()
        {
            if (CurrentLocation != null && CurrentLocation != string.Empty && CurrentLocation !="N/A" && CurrentLocation.Length > 0)
                weatherInfo.GetInfo(CurrentLocation);
            
        }

        static void DrawTextBox()
        {


            SKFont sKFont = new SKFont();

            sKFont.Size = 35;

            SKTextBlob textBlob;
            // the rectangle
            var rect = SKRect.Create(city_x + 80, 10, 620, 42);
            // the brush (fill with white)
            canvas.DrawRect(rect, paintWhiteFill);

            // draw fill

            if ((counter == 0)  )
            {
                textBlob = SKTextBlob.Create("Please wait...", sKFont);
            }
            else
            {
                if (IsValidLocation()) {
                    textBlob = SKTextBlob.Create(CurrentLocation.ToUpper(), sKFont);
                }
                else
                {
                    textBlob = SKTextBlob.Create("N/A", sKFont);
                }
            }           

            canvas.DrawText(textBlob, city_x + 85, city_y + sKFont.Size, paintBlack);

            counter++;


        }
        public static void DrawInfomation()
        {
            SKFont sKFont = new SKFont();
            SKFont sKFontBig = new SKFont();

            sKFont.Size = 35;
            sKFontBig.Size = 85;

            SKTextBlob textBlob;




            canvas.DrawBitmap(bitmapMapBackground, 0, 0);

            textBlob = SKTextBlob.Create("City: ", sKFont);

            canvas.DrawText(textBlob, city_x, city_y + sKFont.Size, paintWhiteFill);

            DrawTextBox();

            //canvas.DrawText(textBlob, city_x + 85, city_y + sKFont.Size, paintWhiteFill);


            textBlob = SKTextBlob.Create(weatherInfo.Temperature, sKFontBig);
            canvas.DrawText(textBlob, 30, 150, paintWhiteFill);



            //TemperatureMax *
            textBlob = SKTextBlob.Create("Temp. max: ", sKFont);
            canvas.DrawText(textBlob, 10, 250, paintWhiteFill);

            textBlob = SKTextBlob.Create(weatherInfo.TemperatureMax, sKFont);
            canvas.DrawText(textBlob, 10 + 200, 250, paintWhiteFill);

            //TemperatureMin *
            textBlob = SKTextBlob.Create("Temp. min: ", sKFont);
            canvas.DrawText(textBlob, 10, 300, paintWhiteFill);

            textBlob = SKTextBlob.Create(weatherInfo.TemperatureMin, sKFont);
            canvas.DrawText(textBlob, 10 + 200, 300, paintWhiteFill);

            //Humidity *
            textBlob = SKTextBlob.Create("Humidity: ", sKFont);
            canvas.DrawText(textBlob, 10, 350, paintWhiteFill);

            textBlob = SKTextBlob.Create(weatherInfo.Humidity, sKFont);
            canvas.DrawText(textBlob, 10 + 200, 350, paintWhiteFill);

            //LabWindspeed *
            textBlob = SKTextBlob.Create("Wind speed: ", sKFont);
            canvas.DrawText(textBlob, 10, 400, paintWhiteFill);

            textBlob = SKTextBlob.Create(weatherInfo.LabWindspeed, sKFont);
            canvas.DrawText(textBlob, 10 + 200, 400, paintWhiteFill);



            //Condition
            textBlob = SKTextBlob.Create("Condition: ", sKFont);
            canvas.DrawText(textBlob, 400, 120, paintWhiteFill);

            textBlob = SKTextBlob.Create(weatherInfo.LabCondtion, sKFont);
            canvas.DrawText(textBlob, 400 + 170, 120, paintWhiteFill);

            //LabDetail
            textBlob = SKTextBlob.Create("Detail: ", sKFont);
            canvas.DrawText(textBlob, 400, 250, paintWhiteFill);

            textBlob = SKTextBlob.Create(weatherInfo.LabDetail, sKFont);
            canvas.DrawText(textBlob, 400 + 170, 250, paintWhiteFill);

            //LabSunset
            textBlob = SKTextBlob.Create("Sunset: ", sKFont);
            canvas.DrawText(textBlob, 400, 300, paintWhiteFill);

            textBlob = SKTextBlob.Create(weatherInfo.LabSunset, sKFont);
            canvas.DrawText(textBlob, 400 + 170, 300, paintWhiteFill);

            //Sunrise
            textBlob = SKTextBlob.Create("Sunrise: ", sKFont);
            canvas.DrawText(textBlob, 400, 350, paintWhiteFill);

            textBlob = SKTextBlob.Create(weatherInfo.LabSunrise, sKFont);
            canvas.DrawText(textBlob, 400 + 170, 350, paintWhiteFill);

            //Sunrise
            textBlob = SKTextBlob.Create("Pressure: ", sKFont);
            canvas.DrawText(textBlob, 400, 400, paintWhiteFill);

            textBlob = SKTextBlob.Create(weatherInfo.LabPressure, sKFont);
            canvas.DrawText(textBlob, 400 + 170, 400, paintWhiteFill);
        }
        public static void DrawWeather()
        {
            if (!keyboard.IsEnabled)
            {

                DrawInfomation();

                var data = bitmapMap.Copy(SKColorType.Rgb565).Bytes;

                displayController.Flush(data);

                GetWeatherInfo();
            }
        }
        public static void EnableWeather()
        {
            if (IsEnabled)
                return;
            IsEnabled = true;
            counter = 0;
            Touch.TouchUpEventHandler += TouchUpEvent;
        }

        public static void DisableWeather()
        {
            if (!IsEnabled)
                return;

            Touch.TouchUpEventHandler -= TouchUpEvent;

            IsEnabled = false;

           
        }

        static VirtualKeyboard keyboard;
        static void TouchUpEvent(int x, int y)
        {
            if (!IsEnabled || counter == 0)
                return;

            if (!keyboard.IsEnabled)
            {
                if (x >= 80 && x <= 620 && y < 70)
                {
                    new Thread(keyboard.Show).Start();
                }
                else
                {
                    DisableWeather();

                    MainMenu.EnableMainMenu();
                }
            }
            else
            {
                keyboard.UpdateKey(x, y);
            }

        }
    }
}
