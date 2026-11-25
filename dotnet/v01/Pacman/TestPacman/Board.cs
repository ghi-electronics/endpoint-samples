using GHIElectronics.Endpoint;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TestPacman.Properties;

namespace TestPacman
{
    public class Board
    {
        public const int COL = 15;
        public const int ROW = 15;
        private const int FPS = 15;
        public const int INTERVAL = 1000 / FPS;
        public Food food;
        public Pacman pacman;
        public Ghost ghost;
        public Player player;
        //public Buttons buttons;
        public Sound audio;
        public int Width { get; set; }
        public int Height { get; set; }

        private SKBitmap bitmap;

        //private Graphics gfx;

        private SKCanvas canvas;

        private Display display;

        public PictureBox boardImage;

        public int[,] map;

        public Board(int width, int height)
        {

            this.Width = width;
            this.Height = height;

            this.bitmap = new SKBitmap(this.Width, this.Height);
            this.canvas = new SKCanvas(bitmap);


            this.display = new Display();

            boardImage = new PictureBox();

            //this.buttons = new Buttons(this);
            this.food = new Food(this);
            this.pacman = new Pacman(this);
            this.ghost = new Ghost(this);
            this.player = new Player(this);

            this.audio = new Sound();            
        }
        public Tuple<int, int> Initialize()
        {
            var img = Resources.Board_1;

            var info = new SKImageInfo(320, 240); // width and height of rect

            boardImage.Image = SKBitmap.Decode(img, info);

            // Initialise Game Board Matrix
            // 10 : collion
            // 01 : food 1
            // 02 : food 2
            // 03 : pacman start
            // 15 : ghost start
            map = new int[,] {
                        {  01, 10, 10, 10, 01, 10, 10, 10, 01, 10, 10, 10, 10, 10, 10 },
                        {  01, 01, 01, 01, 01, 01, 03, 01, 01, 01, 01, 01, 01, 01, 01 },
                        {  01, 10, 10, 10, 02, 10, 10, 10, 10, 10, 01, 10, 10, 10, 01 },
                        {  01, 10, 10, 10, 01, 10, 10, 10, 10, 10, 01, 10, 10, 10, 01 },
                        {  01, 10, 10, 10, 01, 10, 10, 10, 10, 10, 01, 10, 10, 10, 01 },
                        {  01, 01, 01, 01, 01, 01, 01, 02, 01, 01, 01, 10, 10, 10, 01 },
                        {  10, 10, 01, 10, 10, 10, 10, 10, 01, 10, 10, 10, 10, 10, 01 },
                        {  10, 10, 01, 10, 10, 10, 10, 10, 01, 10, 10, 10, 10, 10, 01 },
                        {  10, 10, 01, 10, 10, 10, 10, 10, 01, 10, 10, 10, 10, 10, 01 },
                        {  01, 01, 01, 01, 01, 01, 01, 01, 01, 02, 01, 01, 01, 01, 01 },
                        {  01, 10, 10, 10, 01, 10, 10, 10, 01, 10, 10, 10, 10, 10, 01 },
                        {  01, 10, 10, 10, 02, 10, 10, 10, 01, 10, 10, 10, 10, 10, 01 },
                        {  01, 10, 10, 10, 01, 10, 10, 10, 01, 10, 10, 10, 10, 10, 01 },
                        {  01, 01, 01, 01, 01, 10, 10, 10, 01, 01, 01, 01, 01, 01, 01 },
                        {  01, 10, 10, 10, 01, 10, 10, 10, 01, 10, 10, 10, 01, 10, 15 },
                        //{  01, 10, 10, 10, 01, 01, 01, 01, 01, 10, 10, 10, 01, 10, 15 },
                        //{  01, 10, 10, 10, 01, 10, 10, 10, 02, 10, 10, 10, 01, 10, 15 },
                        //{  01, 10, 10, 10, 01, 10, 10, 10, 01, 01, 01, 01, 01, 10, 15 },
                        //{  01, 10, 10, 10, 01, 10, 10, 10, 01, 10, 10, 10, 10, 10, 10 },
                        //{  01, 01, 01, 01, 01, 01, 01, 01, 01, 10, 10, 10, 10, 10, 10 },


                    };
            int startX = 0;
            int startY = 0;

            for (int y = 0; y < ROW; y++)
            {
                for (int x = 0; x < COL; x++)
                {
                    if (map[y, x] == 3) { startX = x; startY = y; }
                }
            }
            Tuple<int, int> StartLocation = new Tuple<int, int>(startX, startY);

            return StartLocation;
        }

        

        
        public void SetupGame()
        {

            // Create board
            Tuple<int, int> pacmanStartCoordinates = Initialize();

            // Create food
            food.CreateFoodImages();

            // Create ghost
            ghost.Initialize();

            // Create pacman
            pacman.Initialize(pacmanStartCoordinates.Item1, pacmanStartCoordinates.Item2);
        }

        public void Run()
        {


            while (!player.finished)
            {
                Thread.Sleep(1);

                Update();

                // draw the board:
                this.canvas.DrawBitmap(boardImage.Image, 0, 0);

                // Draw food
                for (var y = 0; y < Board.ROW; y++)
                {

                    for (var x = 0; x < Board.COL; x++)
                    {
                        if (food.foodImages[y, x] != null && food.foodImages[y, x].Visible)
                        {
                            this.canvas.DrawBitmap(food.foodImages[y, x].Image, food.foodImages[y, x].Location.X, food.foodImages[y, x].Location.Y);

                        }
                    }
                }

                // Draw pacman
                this.canvas.DrawBitmap(pacman.pacman.Image, pacman.pacman.Location.X, pacman.pacman.Location.Y);


                // Draw ghost
                for (var i = 0; i < Ghost.GHOST_NUM; i++)
                {
                    if (ghost.ghosts[i] != null && ghost.ghosts[i].Visible)
                    {
                        this.canvas.DrawBitmap(ghost.ghosts[i].Image, ghost.ghosts[i].Location.X, ghost.ghosts[i].Location.Y    );
                    }
                }


                var data = bitmap.Copy(SKColorType.Rgb565).Bytes;
                this.display.Flush(data, 0, data.Length);

                //using (var image = SKImage.FromBitmap(bitmap))
                //using (var png = image.Encode(SKEncodedImageFormat.Png, 80))
                //{
                //    // save the data to a stream
                //    using (var stream = File.OpenWrite(Path.Combine("/media/", "2.png")))
                //    {
                //        png.SaveTo(stream);
                //    }
                //}

                //FileSystem.Flush();


            }

            //var font = new Font("arial", 18);

            //var brush = new SolidBrush(Color.White);

            //this.gfx.DrawString("YOU WIN", font, brush, 5, 60);

            //this.display.Flush(this.bitmap);
        }

        public void Update()
        {
            pacman.Move();
            ghost.Move();

        }

        public void OnKeyDown(int key)
        {
            switch (key)
            {
                //case Buttons.UP: pacman.nextDirection = 1; pacman.Move(1); break;
                //case Buttons.RIGHT: pacman.nextDirection = 2; pacman.Move(2); break;
                //case Buttons.DOWN: pacman.nextDirection = 3; pacman.Move(3); break;
                //case Buttons.LEFT: pacman.nextDirection = 4; pacman.Move(4); break;
            }
        }
    }
}
