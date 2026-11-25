using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Devices.Camera;
using GHIElectronics.Endpoint.Devices.Display;
using GHIElectronics.Endpoint.Devices.UsbHost;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Device.Pwm;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EndpointGoogleMap
{
    public static class BackupCamera
    {
        static SKBitmap bitmapMap;

        static DisplayController displayController;

        static UsbHostController usbhostController;

        static Webcam webcam;

        static PwmChannel pwmChannel;

        static SKFont sKFont;

        static SKPaint paintText;

        static SKPaint strokePaintRed;
        static SKPaint strokePaintYellow;

        static SKPath pathRed;
        static SKPath pathYellow;

        static SKTextBlob textBlob;
        static public void Initialize(DisplayController display)
        {
            displayController = display;

            usbhostController = new UsbHostController();

            usbhostController.OnConnectionChangedEvent += UsbhostController_OnConnectionChangedEvent;

            usbhostController.Enable();

            EPM815.Pwm.Initialize(EPM815.Pwm.Pin.PF9);

            pwmChannel = PwmChannel.Create(EPM815.Pwm.GetChipId(EPM815.Pwm.Pin.PF9), EPM815.Pwm.GetChannelId(EPM815.Pwm.Pin.PF9));
            pwmChannel.DutyCycle = 0.25;
            pwmChannel.Frequency = 1000;


            sKFont = new SKFont()
            {
                Size = 24,
            };

            paintText = new SKPaint()
            {
                Color = SKColors.Orange,
                IsAntialias = true,
                StrokeWidth = 2,
                Style = SKPaintStyle.Fill
            };

            strokePaintRed = new SKPaint()
            {
                Style = SKPaintStyle.Stroke,
                Color = SKColors.Red,
                StrokeWidth = 10,
                IsAntialias = true,
            };

            strokePaintYellow = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = SKColors.Yellow,
                StrokeWidth = 10,
                IsAntialias = true,
            };

            pathRed = new SKPath();
            pathYellow = new SKPath();

            textBlob = SKTextBlob.Create("Check surroundings for safety", sKFont);
        }

        static int frameCounter;
        static public void DrawBackupCamera()
        {
            counter = 0;
            soundStarted = false;
            frameCounter = 0;

            while (IsEnabled)
            {
                counter++;

                if (frameCounter != 0) // start only when first frame shown
                {
                    if (counter % 3 == 0)
                    {
                        if (soundStarted)
                        {
                            pwmChannel.Stop();
                            Thread.Sleep(200);
                        }
                        else
                        {
                            pwmChannel.Start();
                            Thread.Sleep(50);
                        }

                        soundStarted = !soundStarted;
                    }
                }

                Thread.Sleep(50);
            }

            pwmChannel.Stop();

        }
        static public void EnableBackupCamera()
        {
            if (IsEnabled)
                return;

            IsEnabled = true;



            if (IsCameraReady)
                webcam.VideoStreamStart();

            Touch.TouchUpEventHandler += TouchUpEvent;
        }

        static public void DisableBackupCamera()
        {
            if (!IsEnabled)
                return;
            Touch.TouchUpEventHandler -= TouchUpEvent;
            IsEnabled = false;

            if (IsCameraReady)
                webcam.VideoStreamStop();

            pwmChannel.Stop();
        }

        static public void StopBeep()
        {
            pwmChannel.Stop();
        }

        static public bool IsEnabled = false;
        static public bool IsCameraReady = false;

        static int counter;
        static bool soundStarted;
        private static void UsbhostController_OnConnectionChangedEvent(UsbHostController sender, DeviceConnectionEventArgs arg)
        {
            if (arg.DeviceStatus == DeviceConnectionStatus.Connected)
            {
                if (arg.Type == GHIElectronics.Endpoint.Devices.Usb.DeviceType.Webcam && arg.DeviceName.IndexOf("video0") > 0 && webcam == null)
                {
                    webcam = new Webcam(arg.DeviceName);

                    var setting = new CameraConfiguration()
                    {
                        Width = 640,
                        Height = 480,
                        ImageFormat = Format.Jpeg,

                    };

                    webcam.Setting = setting;

                    IsCameraReady = true;


                    webcam.FrameReceivedEvent += (a, b) =>
                    {
                        if (!IsEnabled)
                            return;

                        try
                        {
                            var info = new SKImageInfo(webcam.Setting.Width, webcam.Setting.Height); // width and height of rect
                            using (var camBitmap = SKBitmap.Decode(b, info))
                            {
                                var info_fullscreen = new SKImageInfo(displayController.Configuration.Width, displayController.Configuration.Height);

                                bitmapMap = camBitmap.Resize(info_fullscreen, SKFilterQuality.None);

                                using (var canvas = new SKCanvas(bitmapMap))
                                {


                                    
                                    //Red
                                    pathRed.MoveTo(60, 350); // start point
                                    pathRed.LineTo(740, 350); // start point

                                    pathRed.Close(); // make sure path is closed

                                    canvas.DrawPath(pathRed, strokePaintRed);


                                    // Yellow
                                    pathYellow.MoveTo(0, 480); // start point
                                    pathYellow.LineTo(200, 50); // first move to this point
                                    pathYellow.LineTo(600, 50); // move to this point
                                    pathYellow.LineTo(800, 480); // then move to this point

                                    pathYellow.Close(); // make sure path is closed
                                                  // draw the path with paint object                                    
                                    canvas.DrawPath(pathYellow, strokePaintYellow);

                                    canvas.DrawText(textBlob, 260, 450, paintText);

                                    frameCounter++;

                                }


                            }

                            if (bitmapMap != null)
                            {
                                var data = bitmapMap.Copy(SKColorType.Rgb565).Bytes;
                                displayController.Flush(data);
                            }

                            
                        }

                        catch
                        {

                        }




                    };

                    
                }
            }
            else
            {
                IsCameraReady = false;
                if (webcam != null)
                {
                    if (webcam.IsVideoStreaming)
                    {
                        webcam.VideoStreamStop();
                    }

                    webcam.Dispose();
                    webcam = null;
                }
            }
        }

        static void TouchUpEvent(int x, int y)
        {
            if (!IsEnabled)
                return;

            DisableBackupCamera();

            MainMenu.EnableMainMenu();
        }
    }
}
