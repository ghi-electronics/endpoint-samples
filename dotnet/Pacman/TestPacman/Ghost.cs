#define ENABLE_GAME
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestPacman.Properties;

namespace TestPacman
{
#if ENABLE_GAME
    public class Ghost
    {
        public const int GHOST_NUM = 4;

        private ArrayList ghostList = new ArrayList();
        public PictureBox[] ghosts = new PictureBox[GHOST_NUM];

        public int[] states = new int[GHOST_NUM];

        public int[] xCoordinates = new int[GHOST_NUM];
        public int[] yCoordinates = new int[GHOST_NUM];

        private int[] xStarts = new int[GHOST_NUM];
        private int[] yStarts = new int[GHOST_NUM];

        public int[] directions = new int[GHOST_NUM];

        private Random rand = new Random();

        Board gameboard;
        public Ghost(Board gameboard)
        {
            this.gameboard = gameboard;
            Add(Properties.Resources.Ghost_0_1);
            Add(Properties.Resources.Ghost_0_2);
            Add(Properties.Resources.Ghost_0_3);
            Add(Properties.Resources.Ghost_0_4);

            Add(Properties.Resources.Ghost_1_1);
            Add(Properties.Resources.Ghost_1_2);
            Add(Properties.Resources.Ghost_1_3);
            Add(Properties.Resources.Ghost_1_4);

            Add(Properties.Resources.Ghost_2_1);
            Add(Properties.Resources.Ghost_2_2);
            Add(Properties.Resources.Ghost_2_3);
            Add(Properties.Resources.Ghost_2_4);

            Add(Properties.Resources.Ghost_3_1);
            Add(Properties.Resources.Ghost_3_2);
            Add(Properties.Resources.Ghost_3_3);
            Add(Properties.Resources.Ghost_3_4);

            Add(Properties.Resources.Ghost_4);
            Add(Properties.Resources.Ghost_5);

        }

        private void Add(byte[] data)
        {
          

            var info = new SKImageInfo(14, 14); // width and height of rect

            //

            var skimage = SKBitmap.Decode(data, info);

            ghostList.Add(skimage);
        }

        public void Initialize()
        {
            // Create Ghost Image
            for (int x = 0; x < GHOST_NUM; x++)
            {
                ghosts[x] = new PictureBox();

            }

            SetGhosts();
            ResetGhosts();
        }

        public void SetGhosts()
        {
            // Find Ghost locations
            int Amount = -1;
            for (int y = 0; y < Board.ROW; y++)
            {
                for (int x = 0; x < Board.COL; x++)
                {
                    if (this.gameboard.map[y, x] == 15)
                    {
                        Amount++;
                        xStarts[Amount] = x;
                        yStarts[Amount] = y;
                    }
                }
            }
        }

        public void ResetGhosts()
        {
            // Reset Ghost States
            for (int x = 0; x < GHOST_NUM; x++)
            {
                xCoordinates[x] = xStarts[x];
                yCoordinates[x] = yStarts[x];

                ghosts[x].Y = yStarts[x] * Program.BLOCK_SIZE;
                ghosts[x].X = xStarts[x] * Program.BLOCK_SIZE;


                ghosts[x].Location = new Point(ghosts[x].X, ghosts[x].Y);
                ghosts[x].Image = ghostList[x * 4] as SKBitmap;
                directions[x] = 0;
                states[x] = 0;
            }
        }

        private void MoveGhosts(int ghost)
        {
            // Move the ghosts
            if (directions[ghost] == 0)
            {
                if (rand.Next(0, 5) == 3)
                {
                    directions[ghost] = 1;
                }
            }
            else
            {
                bool CanMove = false;
                //OtherDirection(directions[ghost], ghost);

                //while (!CanMove)
                //{
                //    CanMove = CheckDirection(directions[ghost], ghost);
                //    if (!CanMove) { ChangeDirection(directions[ghost], ghost); }

                //}

                if (!CanMove)
                {
                    switch (directions[ghost])
                    {
                        case 1:
                            this.ghosts[ghost].Y -= 4;
                            if ((this.ghosts[ghost].Y % Program.BLOCK_SIZE) == 0)
                            {
                                if (yCoordinates[ghost] > 0)
                                {

                                    yCoordinates[ghost]--;

                                    this.ghosts[ghost].Y = yCoordinates[ghost] * Program.BLOCK_SIZE;
                                    this.ghosts[ghost].X = xCoordinates[ghost] * Program.BLOCK_SIZE;
                                }

                                OtherDirection(directions[ghost], ghost);

                                while (!CanMove)
                                {
                                    CanMove = CheckDirection(directions[ghost], ghost);
                                    if (!CanMove) { ChangeDirection(directions[ghost], ghost); }

                                }
                            }
                            break;

                        case 2:
                            this.ghosts[ghost].X += 4;
                            if ((this.ghosts[ghost].X % Program.BLOCK_SIZE) == 0)
                            {
                                if (xCoordinates[ghost] < Board.COL-1)
                                {
                                    xCoordinates[ghost]++;

                                    this.ghosts[ghost].Y = yCoordinates[ghost] * Program.BLOCK_SIZE;
                                    this.ghosts[ghost].X = xCoordinates[ghost] * Program.BLOCK_SIZE;
                                }

                                OtherDirection(directions[ghost], ghost);

                                while (!CanMove)
                                {
                                    CanMove = CheckDirection(directions[ghost], ghost);
                                    if (!CanMove) { ChangeDirection(directions[ghost], ghost); }

                                }
                            }
                            break;

                        case 3:
                            this.ghosts[ghost].Y += 4;
                            if ((this.ghosts[ghost].Y % Program.BLOCK_SIZE) == 0)
                            {
                                if (yCoordinates[ghost] < Board.COL - 1)
                                {
                                    yCoordinates[ghost]++;

                                    this.ghosts[ghost].Y = yCoordinates[ghost] * Program.BLOCK_SIZE;
                                    this.ghosts[ghost].X = xCoordinates[ghost] * Program.BLOCK_SIZE;
                                }

                                OtherDirection(directions[ghost], ghost);

                                while (!CanMove)
                                {
                                    CanMove = CheckDirection(directions[ghost], ghost);
                                    if (!CanMove) { ChangeDirection(directions[ghost], ghost); }

                                }
                            }
                            break;

                        case 4:
                            this.ghosts[ghost].X -= 4;
                            if ((this.ghosts[ghost].X % Program.BLOCK_SIZE) == 0)
                            {
                                if (xCoordinates[ghost] > 0)
                                {
                                    xCoordinates[ghost]--;

                                    this.ghosts[ghost].Y = yCoordinates[ghost] * Program.BLOCK_SIZE;
                                    this.ghosts[ghost].X = xCoordinates[ghost] * Program.BLOCK_SIZE;
                                }

                                OtherDirection(directions[ghost], ghost);

                                while (!CanMove)
                                {
                                    CanMove = CheckDirection(directions[ghost], ghost);
                                    if (!CanMove) { ChangeDirection(directions[ghost], ghost); }

                                }
                            }
                            break;
                    }

                    this.ghosts[ghost].Location = new Point(this.ghosts[ghost].X, this.ghosts[ghost].Y);
                }
            }

        }

        public void Move()
        {
            for (var i = 0; i < GHOST_NUM; i++)
            {
                MoveGhosts(i);
            }
        }

        private bool CheckDirection(int direction, int ghost)
        {
            // Check if ghost can move to space
            switch (direction)
            {
                case 1: return IsGoodDirection(xCoordinates[ghost], yCoordinates[ghost] - 1, ghost);
                case 2: return IsGoodDirection(xCoordinates[ghost] + 1, yCoordinates[ghost], ghost);
                case 3: return IsGoodDirection(xCoordinates[ghost], yCoordinates[ghost] + 1, ghost);
                case 4: return IsGoodDirection(xCoordinates[ghost] - 1, yCoordinates[ghost], ghost);
                default: return false;
            }
        }

        private bool IsGoodDirection(int x, int y, int ghost)
        {
            // Check if board space can be used
            if (x < 0)
            {
                return false;
            }

            if (x == Board.COL)
            {
                return false;
            }

            if (y < 0)
            {
                return false;
            }

            if (y == Board.ROW)
            {
                return false;
            }


            if (this.gameboard.map[y, x] < 4 || this.gameboard.map[y, x] > 10)
            {
                return true;

                //if (ghosts[ghost].X / 16  < 4 )
            }
            else
            {
                return false;
            }
        }

        private void ChangeDirection(int direction, int ghost)
        {
            // Change the direction of the ghost
            int which = rand.Next(0, 2);
            switch (direction)
            {
                case 1: case 3: if (which == 1) { directions[ghost] = 2; } else { directions[ghost] = 4; }; break;
                case 2: case 4: if (which == 1) { directions[ghost] = 1; } else { directions[ghost] = 3; }; break;
            }
        }

        private void OtherDirection(int direction, int ghost)
        {
            // Check to see if the ghost can move a different direction
            if (this.gameboard.map[yCoordinates[ghost], xCoordinates[ghost]] < 4)
            {
                bool[] dirs = new bool[5];
                int x = xCoordinates[ghost];
                int y = yCoordinates[ghost];
                switch (direction)
                {
                    case 1: case 3: dirs[2] = IsGoodDirection(x + 1, y, ghost); dirs[4] = IsGoodDirection(x - 1, y, ghost); break;
                    case 2: case 4: dirs[1] = IsGoodDirection(x, y - 1, ghost); dirs[3] = IsGoodDirection(x, y + 1, ghost); break;
                }
                int which = rand.Next(0, 5);
                if (dirs[which] == true) { directions[ghost] = which; }

                //for (int i = 0; i < dirs.Length; i++)
                //{
                //    if (!dirs[i]) continue;

                //    directions[ghost] = i;
                //}
            }
        }

        public void CheckForPacman()
        {
            // Check to see if a ghost is on the same block as Pacman
            for (int x = 0; x < GHOST_NUM; x++)
            {
                if (xCoordinates[x] == this.gameboard.pacman.xCoordinate && yCoordinates[x] == this.gameboard.pacman.yCoordinate)
                {
                    switch (states[x])
                    {
                        case 0:
                            this.gameboard.player.Lose();
                            break;
                        case 1:
                            //
                            var img = Resources.eyes;

                            var info = new SKImageInfo(14, 7); // width and height of rect

                            //
                            states[x] = 2;
                            ghosts[x].Image = SKBitmap.Decode(img, info);

                            break;
                    }
                }
            }
        }
    }
#endif
}
