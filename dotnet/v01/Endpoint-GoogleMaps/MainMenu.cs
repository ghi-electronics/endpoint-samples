using GHIElectronics.Endpoint.Devices.Display;
using GHIElectronics.Endpoint.Drivers.FocalTech.FT5xx6;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace EndpointGoogleMap
{
    public static class MainMenu
    {
        static SKBitmap bitmapMap;
        static SKBitmap bitmapMapBackground;

        static DisplayController displayController;

        public static bool IsEnabled = false;


        public static void Initialize(DisplayController display)
        {
            displayController = display;

            var img = Resources.mainmenu_bg;
            var info = new SKImageInfo(displayController.Configuration.Width, displayController.Configuration.Height); // width and height of rect


            bitmapMapBackground = SKBitmap.Decode(img, info);

            bitmapMap = new SKBitmap(displayController.Configuration.Width, displayController.Configuration.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);


        }

        public static void DrawMainMenu()
        {

            using (var screen = new SKCanvas(bitmapMap))
            {
                screen.DrawBitmap(bitmapMapBackground, 0, 0);
                byte[] fontfile = Resources.LCD;
                Stream stream = new MemoryStream(fontfile);

                using (SKPaint text = new SKPaint())
                {
                    text.Color = SKColors.White;
                    text.IsAntialias = true;
                    text.StrokeWidth = 2;
                    text.Style = SKPaintStyle.Fill;
                    text.TextSize = 12;
                    SKFont sKFont = new SKFont();
                    sKFont.Size = 22;
                    SKTextBlob googleMapIconText = SKTextBlob.Create("Google Maps", sKFont);
                    SKTextBlob backupCamIconText = SKTextBlob.Create("Backup Cam", sKFont);
                    SKTextBlob fmRadioIconText = SKTextBlob.Create("FM Radio", sKFont);
                    SKTextBlob weatherText = SKTextBlob.Create("Weather App", sKFont);
                    SKTextBlob infoText = SKTextBlob.Create("Information", sKFont);


                    screen.DrawText(googleMapIconText, 110, 270, text);
                    screen.DrawText(backupCamIconText, 337, 270, text);
                    screen.DrawText(fmRadioIconText, 569, 270, text);
                    screen.DrawText(weatherText, 114, 442, text);
                    screen.DrawText(infoText, 345, 442, text);





                    using (SKPaint presetText = new SKPaint())
                    // Font from Resources




                    using (SKTypeface tf = SKTypeface.FromStream(stream))
                    {

                        SKFont currentFont = new SKFont();
                        currentFont.Size = 153;
                        currentFont.Typeface = tf;

                        SKFont presetButtonsFont = new SKFont();
                        presetButtonsFont.Size = 29;
                        presetButtonsFont.Typeface = tf;


                        // Station Preset Text Properties
                        presetText.Color = SKColors.White;
                        presetText.IsAntialias = true;
                        presetText.StrokeWidth = 2;
                        presetText.Style = SKPaintStyle.Fill;


                        // Draw Date and Time 
                        SKTextBlob dateTime = SKTextBlob.Create(DateTime.Now.ToString(), presetButtonsFont);
                        screen.DrawText(dateTime, 520, 64, presetText);
                    }
                }

                var data = bitmapMap.Copy(SKColorType.Rgb565).Bytes;
                displayController.Flush(data);
            }
        }

        public static void EnableMainMenu()
        {
            if (IsEnabled)
                return;
            IsEnabled = true;

            Touch.TouchUpEventHandler += TouchUpEvent;


        }

        public static void DisableMainMenu()
        {
            if (!IsEnabled)
                return;

            Touch.TouchUpEventHandler -= TouchUpEvent;
            IsEnabled = false;


        }


        static void TouchUpEvent(int x, int y)
        {
            if (!IsEnabled)
                return;

            if (x > 100 && x < 250 && y > 110 && y < 250)
            {
                DisableMainMenu();

                Map.EnableMap();
            }

            else if (x > 330 && x < 470 && y > 110 && y < 250)
            {

                DisableMainMenu();

                BackupCamera.EnableBackupCamera();



                return;
            }


            else if (x > 540 && x < 680 && y > 110 && y < 250)
            {

                DisableMainMenu();

                FMRadio.EnableRadio();


                return;

            }

            else if (x > 100 && x < 250 && y > 280 && y < 430)
            {

                DisableMainMenu();

                Weather.EnableWeather();

                return;

            }

            else if (x > 330 && x < 470 && y > 280 && y < 430)
            {

                DisableMainMenu();

                About.EnableAbout();

                return;

            }

        }
    }
}
