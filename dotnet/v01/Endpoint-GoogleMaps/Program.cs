using SkiaSharp;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using GHIElectronics.Endpoint.Devices.Display;
using GHIElectronics.Endpoint.Core;
using GHIElectronics.Endpoint.Devices.Network;
using GHIElectronics.Endpoint.Devices.Rtc;
using GHIElectronics.Endpoint.Drivers.FocalTech.FT5xx6;
using EndpointGoogleMap;
using System.Runtime.CompilerServices;
using System.Drawing;
using static EndpointGoogleMap.Network;
using System.IO;



SKBitmap bitmap = new SKBitmap(800, 480, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
bitmap.Erase(SKColors.Transparent);
SKCanvas canvas = new SKCanvas(bitmap);

SKPaint whiteColor = new SKPaint()
{
    Color = SKColors.White,
    StrokeWidth = 5,
    Style = SKPaintStyle.Fill,


};

SKPaint blackColor = new SKPaint()
{
    Color = SKColors.Black,
    Style = SKPaintStyle.Fill,


};

SKFont sKFont = new SKFont();

sKFont.Size = 28;


SKTextBlob textBlob;

// the rectangle
var rect = SKRect.Create(395, 125, 100, 50);

//Initialize Display
Display.Initialize();


//Initialize Touch
Touch.Initialize();


//Initialize Network
Network.Initialize(Display.Screen);

Reconnect:
bitmap.Erase(SKColors.Black);


textBlob = SKTextBlob.Create("Connecting WiFi....", sKFont);
canvas.DrawText(textBlob, 50, 150, whiteColor);
Display.Screen.Flush(bitmap.Copy(SKColorType.Rgb565).Bytes);


new Thread(ConnectWiFi).Start();

var cnt = 0;
while (Network.Status == Network.NetworkStatus.Connecting || Network.Status == NetworkStatus.Reconnect)
{

    canvas.DrawRect(rect, blackColor);
    textBlob = SKTextBlob.Create(cnt.ToString(), sKFont);
    canvas.DrawText(textBlob, 400, 150, whiteColor);
    Display.Screen.Flush(bitmap.Copy(SKColorType.Rgb565).Bytes);

    Thread.Sleep(1000);
    cnt++;

}



if (Network.Status == NetworkStatus.Connected)
{
    canvas.DrawRect(rect, blackColor);
    textBlob = SKTextBlob.Create("OK", sKFont);
    canvas.DrawText(textBlob, 400, 150, whiteColor);
    Display.Screen.Flush(bitmap.Copy(SKColorType.Rgb565).Bytes);
}
else
{
    if (Network.Status == NetworkStatus.Error)
    {
        canvas.DrawRect(rect, blackColor);
        textBlob = SKTextBlob.Create("Bad or no WiFi module detected in USBH port.", sKFont);
        canvas.DrawText(textBlob, 50, 200, whiteColor);

        textBlob = SKTextBlob.Create("Check WiFi module and Reset the device.", sKFont);
        canvas.DrawText(textBlob, 50, 250, whiteColor);

        Display.Screen.Flush(bitmap.Copy(SKColorType.Rgb565).Bytes);

        while (true)
        {
            Thread.Sleep(10);
        }
    }
    else
    {
        Network.DrawUpdateWiFi();

        goto Reconnect;
    }

}





textBlob = SKTextBlob.Create("Synchronizing device time...", sKFont);
canvas.DrawText(textBlob, 50, 200, whiteColor);

Display.Screen.Flush(bitmap.Copy(SKColorType.Rgb565).Bytes);

// Initialize time
var now = TimeSNTP.Initialize();

//var now = DateTime.Now;


textBlob = SKTextBlob.Create(now.ToString(), sKFont);
canvas.DrawText(textBlob, 420, 200, whiteColor);

textBlob = SKTextBlob.Create("Initializing map...", sKFont);
canvas.DrawText(textBlob, 50, 250, whiteColor);

Display.Screen.Flush(bitmap.Copy(SKColorType.Rgb565).Bytes);

// Initialize Map
Map.Initialize(Display.Screen);

textBlob = SKTextBlob.Create("OK", sKFont);
canvas.DrawText(textBlob, 400, 250, whiteColor);

textBlob = SKTextBlob.Create("Initializing menu...", sKFont);
canvas.DrawText(textBlob, 50, 300, whiteColor);

Display.Screen.Flush(bitmap.Copy(SKColorType.Rgb565).Bytes);

MainMenu.Initialize(Display.Screen);

textBlob = SKTextBlob.Create("OK", sKFont);
canvas.DrawText(textBlob, 400, 300, whiteColor);

textBlob = SKTextBlob.Create("Initialize camera....", sKFont);
canvas.DrawText(textBlob, 50, 350, whiteColor);
Display.Screen.Flush(bitmap.Copy(SKColorType.Rgb565).Bytes);

BackupCamera.Initialize(Display.Screen);

textBlob = SKTextBlob.Create("OK", sKFont);
canvas.DrawText(textBlob, 400, 350, whiteColor);

Display.Screen.Flush(bitmap.Copy(SKColorType.Rgb565).Bytes);

FMRadio.Initialize(Display.Screen);

Weather.Initialize(Display.Screen);

Map.EnableMap(); // start map first

About.Initialize(Display.Screen);



while (true)
{
    if (Map.IsEnabled)
    {
        if (Map.StatusChanged != MapUpdateMode.None)
        {
            await Map.DrawMap();
            Map.StatusChanged = MapUpdateMode.None;
        }
    }
    else if (MainMenu.IsEnabled)
    {
        MainMenu.DrawMainMenu();


    }
    else if (BackupCamera.IsEnabled)
    {
        BackupCamera.DrawBackupCamera();

    }
    else if (FMRadio.IsEnabled)
    {
        FMRadio.DrawRadio();

    }
    else if (Weather.IsEnabled)
    {
        Weather.DrawWeather();
    }
    else if (About.IsEnabled)
    {
        About.DrawAbout();
    }
    Thread.Sleep(10);
}
//Touch Events


