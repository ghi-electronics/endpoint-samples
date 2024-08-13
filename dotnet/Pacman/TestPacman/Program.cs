using TestPacman;
using TestPacman.Properties;
using Iot.Device.Button;
using Iot.Device.Buzzer;

using System.Device.Gpio;
using System.Device.Spi;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TestPacman
{
    public class Program
    {
        public static int BLOCK_SIZE = 16;
        static void Main()
        {
            const int SCREEN_WIDTH = 320;
            const int SCREEN_HEIGHT = 240;

            var board = new Board(SCREEN_WIDTH, SCREEN_HEIGHT);

            board.SetupGame();
            board.Run();

        }
    }
}
