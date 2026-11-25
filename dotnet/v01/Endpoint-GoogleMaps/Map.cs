using GHIElectronics.Endpoint.Devices.Display;
using GHIElectronics.Endpoint.Drivers.FocalTech.FT5xx6;
using GHIElectronics.Endpoint.Drivers.VirtualKeyboard;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace EndpointGoogleMap
{
    public enum MapUpdateMode
    {
        None = 0,
        UpdateNewMap = 1,
        RefreshScreenOnly = 2,
    };
    public static class Map
    {
        static DisplayController displayController;
      

        //GoogleMaps
        static int imageWidth = 640;
        static int imageHeight = 400;
        static int zoomLevel = 15;
        static double latitude = 42.527714;
        static double longitude = -83.1036585;
        static string googleAPISignature = "Add Your Google Key";
        public static MapUpdateMode StatusChanged = MapUpdateMode.UpdateNewMap;
        static string mapType = "roadmap";
        static VirtualKeyboard keyboard;
        static SKBitmap bitmapMap;
        static SKBitmap webBitmap;

        static SKFont sKFont;

        static SKCanvas screenCanvas;
        static SKPaint colorBlack;
        static SKPaint colorWhite;



        public static bool IsEnabled = false;

        static public void Initialize(DisplayController display)
        {
            displayController = display;

            keyboard = new VirtualKeyboard(display);
            keyboard.OnClose += Keyboard_OnClose;

            

            bitmapMap = new SKBitmap(displayController.Configuration.Width, displayController.Configuration.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            screenCanvas = new SKCanvas(bitmapMap);

            var backgroundImage = Resources.map_offline_background;
            var backgroundImageInfo = new SKImageInfo(800, 480);
            var background = SKBitmap.Decode(backgroundImage, backgroundImageInfo);
            screenCanvas.DrawBitmap(background, 0, 0);
            sKFont = new SKFont()
            {
                Size = 24
            };
            colorBlack = new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.Black };
            colorWhite = new SKPaint() { Style = SKPaintStyle.Fill, Color = SKColors.White };

           
        }

        private static void Keyboard_OnClose()
        {
            StatusChanged = MapUpdateMode.RefreshScreenOnly;
        }

        static void TouchUpEvent(int x, int y)
        {

            Console.WriteLine("X =" + x.ToString());
            Console.WriteLine("Y =" + y.ToString());

            if (!IsEnabled)
                return;

            if (!keyboard.IsEnabled)
            {
                //Touch Zoom In
                if (x >= 725 && x <= 780 && y >= 64 && y <= 123)
                {
                    zoomLevel = zoomLevel + 1;
                    Console.WriteLine("Zoom Level " + zoomLevel.ToString());
                    StatusChanged = MapUpdateMode.UpdateNewMap;
                    return;
                }
                //Touch Zoom Out
                if (x >= 725 && x <= 780 && y >= 397 && y <= 462)
                {
                    zoomLevel = zoomLevel - 1;
                    Console.WriteLine("Zoom Level " + zoomLevel.ToString());
                    StatusChanged = MapUpdateMode.UpdateNewMap;
                    return;
                }
                //Touch Layer Style Road Map
                if (x >= 30 && x <= 70 && y >= 90 && y <= 130)
                {
                    mapType = "roadmap";
                    StatusChanged = MapUpdateMode.UpdateNewMap;
                    return;
                }
                //Touch Layer Style Satellite Map
                if (x >= 30 && x <= 70 && y >= 180 && y <= 230)
                {
                    mapType = "satellite";
                    StatusChanged = MapUpdateMode.UpdateNewMap;
                    return;
                }
                //Touch Layer Style 3
                if (x >= 30 && x <= 70 && y >= 280 && y <= 330)
                {
                    mapType = "hybrid";
                    StatusChanged = MapUpdateMode.UpdateNewMap;
                    return;
                }

                //Touch Layer Style 3
                if (x >= 30 && x <= 70 && y >= 410 && y <= 450)
                {
                    Console.WriteLine("MainMenu Selected");
                    //mapType = "mainMenu";
                    //statusChanged = true;

                    DisableMap();

                    StatusChanged = MapUpdateMode.RefreshScreenOnly;

                    MainMenu.EnableMainMenu();
                    return;
                }

                // Add keyboard
                if (y < 55)
                {
                    if (!keyboard.IsEnabled)
                    {
                        new Thread(keyboard.Show).Start();
                    }


                }

               
            }
            else
            {
                keyboard.UpdateKey(x, y);


            }
        }

        public static void DisableMap()
        {
            if (!IsEnabled)
                return;

            Touch.TouchUpEventHandler -= TouchUpEvent;
            IsEnabled = false;  
        }

        public static void EnableMap()
        {

            if (IsEnabled)
                return;

            

            IsEnabled = true;

            Touch.TouchUpEventHandler += TouchUpEvent;
        }
        public static async Task DrawMap()
        {

            //bitmapMap.Erase(SKColors.Transparent);
            
            //Initialize Screen Canvas
            //using (var screen = new SKCanvas(bitmapMap))
            //{

            //Intialize Background 
            //var backgroundImage = Resources.background;
            //var backgroundImageInfo = new SKImageInfo(800, 480);
            //var background = SKBitmap.Decode(backgroundImage, backgroundImageInfo);
            //screen.DrawBitmap(background, 0, 0);

            HttpClient httpClient = new HttpClient();
            //Google Static Map URL
        
            string url = string.Empty;
            if (keyboard != null && keyboard.Text != null && keyboard.Text.Length > 0)
            {

                url = "https://maps.googleapis.com/maps/api/staticmap?size="+ imageWidth.ToString() +"x"+ imageHeight.ToString()+ "&maptype=" + mapType + "&zoom=" +zoomLevel.ToString()+"&maptype=roadmap&markers=size:mid%7Ccolor:red%7C"+keyboard.Text+ "&key=" + googleAPISignature;


                Console.Write(url);

                StatusChanged = MapUpdateMode.UpdateNewMap;
            }
            else
            {
                 url = "https://maps.googleapis.com/maps/api/staticmap?center=Chicago&zoom=" + zoomLevel.ToString() + "&size=" + imageWidth.ToString() + "x" + imageHeight.ToString() + "&maptype=" + mapType + "&key=" + googleAPISignature;
            }


            try
            {
                if (StatusChanged == MapUpdateMode.UpdateNewMap || webBitmap == null)
                {
                    using (Stream stream = await httpClient.GetStreamAsync(url))
                    using (MemoryStream memStream = new MemoryStream())
                    {
                        await stream.CopyToAsync(memStream);
                        memStream.Seek(0, SeekOrigin.Begin);
                        var info = new SKImageInfo(imageWidth, imageHeight);
                        webBitmap = SKBitmap.Decode(memStream, info);
                       
                    };
                }

                screenCanvas.DrawBitmap(webBitmap, 80, 60);

                var rect = SKRect.Create(75, 15, 800-(75*2), 30);
                {
                    // the brush (fill with white)
                    screenCanvas.DrawRect(rect, colorWhite);
                };

                if (keyboard.Text.Length > 0)
                {                    
                    var text = keyboard.Text.Length < 40 ? keyboard.Text : keyboard.Text.Substring(0, 40) + "...";
                    using (var textBlob = SKTextBlob.Create(text, sKFont))
                    {
                        screenCanvas.DrawText(textBlob, 80, 40, colorBlack);
                    }
                }
            }
            catch
            {
                Console.WriteLine("Web Request Error");
            }
            ////Intialize Buttons
            //var zoomInImage = Resources.buttonZoomIn;
            //var zoomInImageInfo = new SKImageInfo(37, 37);
            //var zoomInButton = SKBitmap.Decode(zoomInImage, zoomInImageInfo);
            //var zoomOutImage = Resources.buttonZoomOut;
            //var zoomOutImageInfo = new SKImageInfo(37, 37);
            //var zoomOutButton = SKBitmap.Decode(zoomOutImage, zoomOutImageInfo);
            //var roadmapImage = Resources.roadmap;
            //var roadmapImageInfo = new SKImageInfo(43, 40);
            //var roadmapButton = SKBitmap.Decode(roadmapImage, roadmapImageInfo);
            //var satelliteImage = Resources.satellite;
            //var satelliteImageInfo = new SKImageInfo(43, 40);
            //var satelliteButton = SKBitmap.Decode(satelliteImage, satelliteImageInfo);
            //var hybridImage = Resources.hybrid;
            //var hybridImageInfo = new SKImageInfo(43, 40);
            //var hybridButton = SKBitmap.Decode(hybridImage, hybridImageInfo);
            //screen.DrawBitmap(zoomInButton, 430, 35);
            //screen.DrawBitmap(zoomOutButton, 430, 222);
            //screen.DrawBitmap(roadmapButton, 16, 218);
            //screen.DrawBitmap(satelliteButton, 66, 218);
            //screen.DrawBitmap(hybridButton, 116, 218);
            var data = bitmapMap.Copy(SKColorType.Rgb565).Bytes;
            displayController.Flush(data);
            Thread.Sleep(1);
            //}
        }
    }
}
