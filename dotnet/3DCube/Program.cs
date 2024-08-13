//#define ENDPOINT
//#define RPI
#define BBB
using GHIElectronics.Endpoint.Core;

using GHIElectronics.Endpoint.Drivers.HiLetgo.ILI9341;
using SkiaSharp;
using System.Device.Spi;
using System.Drawing;
using static GHIElectronics.Endpoint.Drivers.HiLetgo.ILI9341.ILI9341;

namespace Pacman
{
    internal class Program
    {
        class Vector3
        {
            public double X;
            public double Y;
            public double Z;
            public Vector3(double X, double Y, double Z)
            {
                this.X = X;
                this.Y = Y;
                this.Z = Z;
            }
        }

        class Vector2
        {
            public double X;
            public double Y;
            public Vector2(double X, double Y)
            {
                this.X = X;
                this.Y = Y;
            }
        }

        static void Translate3Dto2D(Vector3[] Points3D, Vector2[] Points2D, Vector3 Rotate, Vector3 Position)
        {
            int OFFSETX = 160;
            int OFFSETY = 80;
            int OFFSETZ = 50;

            double sinax = Math.Sin(Rotate.X * Math.PI / 180);
            double cosax = Math.Cos(Rotate.X * Math.PI / 180);
            double sinay = Math.Sin(Rotate.Y * Math.PI / 180);
            double cosay = Math.Cos(Rotate.Y * Math.PI / 180);
            double sinaz = Math.Sin(Rotate.Z * Math.PI / 180);
            double cosaz = Math.Cos(Rotate.Z * Math.PI / 180);

            for (int i = 0; i < 8; i++)
            {
                double x = Points3D[i].X;
                double y = Points3D[i].Y;
                double z = Points3D[i].Z;

                double yt = y * cosax - z * sinax;  // rotate around the x axis
                double zt = y * sinax + z * cosax;  // using the Y and Z for the rotation
                y = yt;
                z = zt;

                double xt = x * cosay - z * sinay;  // rotate around the Y axis
                zt = x * sinay + z * cosay;         // using X and Z
                x = xt;
                z = zt;

                xt = x * cosaz - y * sinaz;         // finally rotate around the Z axis
                yt = x * sinaz + y * cosaz;         // using X and Y
                x = xt;
                y = yt;

                x = x + Position.X;                 // add the object position offset
                y = y + Position.Y;                 // for both x and y
                z = z + OFFSETZ - Position.Z;       // as well as Z

                Points2D[i].X = (x * 160 / z) + OFFSETX;
                Points2D[i].Y = (y * 160 / z) + OFFSETY;
                
            }
        }
        static void Main(string[] args)
        {





#if ENDPOINT
        EPM815.Spi.Initialize(EPM815.Spi.Spi1, 8192);
        int csPin = EPM815.Gpio.Pin.PA14;
        int dataControlPin = EPM815.Gpio.Pin.PF14;
        int resetPin = EPM815.Gpio.Pin.PF4;
        var setting = ILI9341Controller.GetConnectionSettings(EPM815.Spi.Spi1);
#endif
#if RPI
        int csPin = 2;
        int resetPin = 3;
        int dataControlPin = 4;
        var setting = ILI9341Controller.GetConnectionSettings(0);
#endif
#if BBB
        int csPin = 66;
        int resetPin = 69;
        int dataControlPin = 45;
        var setting = ILI9341Controller.GetConnectionSettings(0);

        //ls -l /dev/spidev*
        //sudo groupadd spi
        //sudo adduser debian spi
        //sudo chgrp spi /dev/spidev0.0
        //sudo chmod 660 /dev/spidev0.0
        // config-pin P9.18 spi
        // config-pin P9.22 spi_sclk

#endif




            var spidev = SpiDevice.Create(setting);

            var display = new ILI9341Controller(spidev, csPin, dataControlPin, resetPin);

            display.Enable();
            


            var bitmap = new SKBitmap(320, 240, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            var canvas = new SKCanvas(bitmap);


            // our object in 3D space
            Vector3[] cube_points = new Vector3[8]
            {
            new Vector3(10,10,-10),
            new Vector3(-10,10,-10),
            new Vector3(-10,-10,-10),
            new Vector3(10,-10,-10),
            new Vector3(10,10,10),
            new Vector3(-10,10,10),
            new Vector3(-10,-10,10),
            new Vector3(10,-10,10),
            };

            // what we get back in 2D space!
            Vector2[] cube2 = new Vector2[8]
            {
            new Vector2(0,0),
            new Vector2(0,0),
            new Vector2(0,0),
            new Vector2(0,0),
            new Vector2(0,0),
            new Vector2(0,0),
            new Vector2(0,0),
            new Vector2(0,0),
            };

            // the connections between the "dots"
            int[] start = new int[12] { 0, 1, 2, 3, 4, 5, 6, 7, 0, 1, 2, 3 };
            uint[] colors = new uint[12] { 0xFF000, 0xFF000, 0x00FF00, 0x00FF00, 0x0000FF, 0x0000FF, 0xFF00FF, 0xFF00FF, 0xFFFF00, 0xFFFF00, 0x00FFFF, 0x00FFFF, };
            int[] end = new int[12] { 1, 2, 3, 0, 5, 6, 7, 4, 4, 5, 6, 7 };

            Vector3 rot = new Vector3(0, 0, 0);
            Vector3 pos = new Vector3(0, 0, 0);

            int x = 0;
            int y = 0;
            int direction1 = 1;

            

            while (true)
            {
                double accelX = x;
                double accelY = y;
                canvas.Clear(0xFFFFFF);

                rot.X = 360 - accelY;
                rot.Y = accelX;
    
                Translate3Dto2D(cube_points, cube2, rot, pos);

                for (int i = 0; i < start.Length; i++)
                {    // draw the lines that make up the object
                    int vertex = start[i];                  // temp = start vertex for this line
                    int sx = (int)cube2[vertex].X;          // set line start x to vertex[i] x position
                    int sy = (int)cube2[vertex].Y;          // set line start y to vertex[i] y position
                    vertex = end[i];                        // temp = end vertex for this line
                    int ex = (int)cube2[vertex].X;          // set line end x to vertex[i+1] x position
                    int ey = (int)cube2[vertex].Y;          // set line end y to vertex[i+1] y position

                    using (SKPaint paint = new SKPaint())
                    {
                        paint.Color = SKColors.Blue;
                        paint.IsAntialias = true;
                        paint.StrokeWidth = 3;
                        paint.Style = SKPaintStyle.Stroke;
                        canvas.DrawLine(sx, sy, ex, ey, paint); //arguments are x position, y position, radius, and paint
                    }

                }



                using (SKPaint paint2 = new SKPaint())
                {

                    paint2.Color = SKColors.Blue;
                    paint2.IsAntialias = true;
                    paint2.StrokeWidth = 3;
                    paint2.Style = SKPaintStyle.Fill;


                    SKFont sKFont = new SKFont();

                    sKFont.Size = 24;


                    SKTextBlob textBlob = SKTextBlob.Create("WE LOVE .NET", sKFont);

                    canvas.DrawText(textBlob, 60, 200, paint2);
                }

                var dataBitmap = bitmap.Copy(SKColorType.Rgb565).Bytes;
                display.DrawBuffer(dataBitmap, 0 , dataBitmap.Length);
                Thread.Sleep(1);

                x += 5 * direction1;
                y += 5 * direction1;


                if (x >= 360)
                {
                    direction1 = -1;

                }
                else if (x <= 5)
                {
                    direction1 = 1;
                }

                

               
            }



        }
    }
}
