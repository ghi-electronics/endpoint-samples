using GHIElectronics.Endpoint.Devices.Display;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static EndpointGoogleMap.Touch;

namespace EndpointGoogleMap
{
    public static class About
    {
        static DisplayController displayController;
        static SKBitmap bitmapMap;




        public static bool IsEnabled = false;

        public static SKCanvas canvas;
        public static void Initialize(DisplayController display)
        {
            displayController = display;

            var img = Resources.about_bg;
            var info = new SKImageInfo(displayController.Configuration.Width, displayController.Configuration.Height); // width and height of rect
            bitmapMap = SKBitmap.Decode(img, info);            
            canvas = new SKCanvas(bitmapMap);
        }

        static void DrawText(SKCanvas canvas, string text, SKRect rect, SKPaint paint)
        {
            float spaceWidth = paint.MeasureText(" ");
            float wordX = rect.Left;
            float wordY = rect.Top + paint.TextSize;
            foreach (string word in text.Split(' '))
            {
                float wordWidth = paint.MeasureText(word);
                if (wordWidth <= rect.Right - wordX)
                {
                    canvas.DrawText(word, wordX, wordY, paint);
                    wordX += wordWidth + spaceWidth;
                }
                else
                {
                    wordY += paint.FontSpacing;
                    wordX = rect.Left;
                }
            }
        }
        public static void DrawAbout()
        {
            //var paintWhiteFill = new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.White, TextSize = 35 };
            //var paintBlack = new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.Black };



            //var rect = new SKRect(100, 100, 600, 280);

            //canvas.DrawRect(rect, paintBlack);

            //DrawText(canvas, "This application is developed full .NET,C# by GHI Electronics team.", rect, paintWhiteFill);

            var data = bitmapMap.Copy(SKColorType.Rgb565).Bytes;

            displayController.Flush(data);


        }
        public static void EnableAbout()
        {
            if (IsEnabled)
                return;
            IsEnabled = true;
 
            Touch.TouchUpEventHandler += TouchUpEvent;
        }

        public static void DisableAbout()
        {
            if (!IsEnabled)
                return;

            Touch.TouchUpEventHandler -= TouchUpEvent;

            IsEnabled = false;


        }

        static void TouchUpEvent(int x, int y)
        {
            if (!IsEnabled )
                return;

            DisableAbout();

            MainMenu.EnableMainMenu();
        }
    }
}
