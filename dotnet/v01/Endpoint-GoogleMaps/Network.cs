using GHIElectronics.Endpoint.Devices.Display;
using GHIElectronics.Endpoint.Devices.Network;
using GHIElectronics.Endpoint.Drivers.VirtualKeyboard;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static EndpointGoogleMap.Touch;

namespace EndpointGoogleMap
{
    public static class Network
    {
        public delegate void ConnectionChangedHandler(bool success);
        public delegate void ConnectionErrorHandler(string error);

        public static event ConnectionChangedHandler ConnectionChangedEventHandler;
        public static event ConnectionErrorHandler ConnectionErrorEventHandler;

        static DisplayController displayController;

        static NetworkController network;

        public static string NetworkError = string.Empty;

        static SKCanvas canvas;
        static SKBitmap bitmap;

        static SKPaint whiteColor;
        static SKPaint blackColor;

        //static SKFont sKFont28;

        public static int NetworkReady { get; private set; } = 0;

        public const int TIMEOUT = 30;

        public enum NetworkStatus
        {
            Connecting = 0,
            Connected = 1,
            Error = 2,
            Timeout = 3,
            Reconnect = 4,
        }

        public static NetworkStatus Status { get; private set; } = NetworkStatus.Connecting;
        public static void Initialize(DisplayController display)
        {

            displayController = display;

            bitmap = new SKBitmap(displayController.Configuration.Width, displayController.Configuration.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            bitmap.Erase(SKColors.Transparent);
            canvas = new SKCanvas(bitmap);

            whiteColor = new SKPaint()
            {
                Color = SKColors.White,

                Style = SKPaintStyle.Fill,


            };

            blackColor = new SKPaint()
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Fill,


            };

           


            keyboard = new VirtualKeyboard(display);

            keyboard.OnClose += Keyboard_OnClose;

            if (File.Exists(ssid_file))
            {
                SsidText = File.ReadAllText(ssid_file);
            }
            else
            {
                SsidText = "Hacked";
            }

            if (File.Exists(pass_file))
            {
                PasswordText = File.ReadAllText(pass_file);
            }
            else
            {
                PasswordText = "hackme123";
            }
        }

        const string ssid_file = "/root/wf_ssid.txt";
        const string pass_file = "/root/wf_pass.txt";

        private static void Keyboard_OnClose()
        {
            if (ssid_input)
            {
                SsidText = keyboard.Text;
            }
            else if (password_input)
            {
                PasswordText = keyboard.Text;
            }
        }

        static string SsidText = string.Empty;
        static string PasswordText = string.Empty;
        static public void ConnectWiFi()
        {
            //Mobile Phone Hotspot
            //var networkSetting = new WiFiNetworkInterfaceSettings
            //{
            //    Ssid = "Hacked",
            //    Password = "hackme123",
            //    DhcpEnable = true,
            //};



            ////Office Wifi
            var networkSetting = new WiFiNetworkInterfaceSettings
            {
                Ssid = SsidText,
                Password = PasswordText,
                DhcpEnable = true,
            };

            Status = NetworkStatus.Connecting;

            var networkType = NetworkInterfaceType.WiFi;

            try
            {
                Thread.Sleep(10); // Yeild to back intialize page and draw text
  
                network = new NetworkController(networkType, networkSetting);
                network.NetworkLinkConnectedChanged += Network_NetworkLinkConnectedChanged;
            }
            catch (Exception ex)
            {
                Status = NetworkStatus.Error;

                return;


            }

         

            new Thread(network.Enable).Start();
            var cnt = 0;

            while (Status == NetworkStatus.Connecting)
            {
                cnt++;

                Thread.Sleep(1000);

                if (cnt == TIMEOUT)
                {

                   

                    Status = NetworkStatus.Timeout;
                    break;
                }

            }

            if (Status == NetworkStatus.Connected)
            {
                // save current ssid and pass
                File.WriteAllText(ssid_file, SsidText);
                File.WriteAllText(pass_file, PasswordText);

                GHIElectronics.Endpoint.FileSystem.Flush(); 
                
            }
            else
            {
                network.Disable();
                network.NetworkLinkConnectedChanged -= Network_NetworkLinkConnectedChanged;
                network.Dispose();
            }
        }

        private static void Network_NetworkLinkConnectedChanged(NetworkController sender, NetworkLinkConnectedChangedEventArgs e)
        {
            if (e.Connected)
            {
                Console.WriteLine("Connected");
                Status = NetworkStatus.Connected;
            }
            else
            {
                Console.WriteLine("Disconnected");
            }
        }

        static VirtualKeyboard keyboard;
        static public void DrawUpdateWiFi()
        {


            SKFont sKFont28 = new SKFont()
            {
                Size = 28

            };

            SKFont sKFont24 = new SKFont()
            {
                Size = 24

            };



            SKTextBlob textBlob;

            // the rectangle
            var rect1 = SKRect.Create(350, 120, 300, 50);
            var rect2 = SKRect.Create(350, 220, 300, 50);
            var rect3 = SKRect.Create(400, 320, 200, 50);
            var textBlobssid = SKTextBlob.Create("SSID:", sKFont28);
            var textBlobpwd = SKTextBlob.Create("Password:", sKFont28);

           


            var textconnect = SKTextBlob.Create("Connect", sKFont28);
            var text = SKTextBlob.Create("Could not connect to WiFi network. Try again or rebuild firmware.", sKFont24);

            Touch.TouchUpEventHandler += TouchUpEvent;
            while (true)
            {
                if (!keyboard.IsEnabled)
                {
                    canvas.DrawText(text, 50, 50, whiteColor);

                    canvas.DrawText(textBlobssid, 200, 150, whiteColor);
                    canvas.DrawRect(rect1, whiteColor);

                    canvas.DrawText(textBlobpwd, 200, 250, whiteColor);
                    canvas.DrawRect(rect2, whiteColor);

                    canvas.DrawRect(rect3, whiteColor);
                    canvas.DrawText(textconnect, 440, 352, blackColor);

                    if (SsidText != null && SsidText.Length > 0)
                    {
                        var t_ssid = SKTextBlob.Create(SsidText, sKFont24);
                        canvas.DrawText(t_ssid, 360, 150, blackColor);
                    }

                    if (PasswordText != null && PasswordText.Length > 0)
                    {
                        var t_pwd = SKTextBlob.Create(PasswordText, sKFont24);
                        canvas.DrawText(t_pwd, 360, 250, blackColor);
                    }

                    Display.Screen.Flush(bitmap.Copy(SKColorType.Rgb565).Bytes);

                    if (Status == NetworkStatus.Reconnect)
                        break;


                }

                Thread.Sleep(10);
            }

            Touch.TouchUpEventHandler -= TouchUpEvent;

        }

        static bool ssid_input = false;
        static bool password_input = false;

       
        static void TouchUpEvent(int x, int y)
        {

            if (!keyboard.IsEnabled)
            {
                ssid_input = false;
                password_input = false;
                if (x > 350 && x < 650 && y > 120 && y < 170)
                {
                    // SSID
                    ssid_input = true;
                    keyboard.Clear();
                    new Thread(keyboard.Show).Start();
                }

                else if (x > 350 && x < 650 && y > 220 && y < 270)
                {
                    // pass
                    password_input = true;
                    keyboard.Clear();
                    new Thread(keyboard.Show).Start();
                }

                else if (x > 400 && x < 600 && y > 320 && y < 370)
                {

                    Status = NetworkStatus.Reconnect;
                }



            }
            else
            {
                keyboard.UpdateKey(x, y);
            }


        }
    }
}
