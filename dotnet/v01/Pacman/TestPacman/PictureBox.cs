using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestPacman
{
    public class PictureBox
    {
        public SKBitmap Image { get; set; }
        public Point Location { get; set; }
        public int Y { get; set; }
        public int X { get; set; }

        public bool Visible { get; set; } = true;

    }
}
