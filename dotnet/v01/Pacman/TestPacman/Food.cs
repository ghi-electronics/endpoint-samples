#define ENABLE_GAME
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestPacman.Properties;

namespace TestPacman
{
#if ENABLE_GAME
    public class Food
    {

        public PictureBox[,] foodImages = new PictureBox[Board.ROW, Board.COL];

        public int amount = 0;

        Board gameboard;
        public Food(Board gameboard)
        {
            this.gameboard = gameboard;
        }
        public void CreateFoodImages( )
        {
            for (int y = 0; y < Board.ROW; y++)
            {
                for (int x = 0; x < Board.COL; x++)
                {
                    if (this.gameboard.map[y, x] == 1 || this.gameboard.map[y, x] == 2)
                    {
                        foodImages[y, x] = new PictureBox();
                        foodImages[y, x].Location = new Point(x * Program.BLOCK_SIZE, y * Program.BLOCK_SIZE);
                        if (this.gameboard.map[y, x] == 1)
                        {
                            var img = Resources.Block_1;

                            var info = new SKImageInfo(14, 14); // width and height of rect

                            foodImages[y, x].Image = SKBitmap.Decode(img, info);
                            amount++;
                        }
                        else
                        {
                            var img = Resources.Block_2;

                            var info = new SKImageInfo(14, 14); // width and height of rect
                            foodImages[y, x].Image = SKBitmap.Decode(img, info);
                        }
                    }
                }
            }
        }

        public void EatSmallFood(int y, int x)
        {
            // Eat food
            foodImages[y, x].Visible = false;
            this.gameboard.map[y, x] = 0;

            amount--;
            if (amount < 1)
            {
                this.gameboard.player.Won();
            }

            this.gameboard.audio.Play(500, 5);
         

        }

        public void EatBigFood(int y, int x)
        {
            // Eat food
            foodImages[y, x].Visible = false;

            this.gameboard.map[y, x] = 0;

        }
    }
#endif
}
