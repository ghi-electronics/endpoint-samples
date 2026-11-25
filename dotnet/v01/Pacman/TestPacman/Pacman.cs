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
#if ENABLE_GAME
namespace TestPacman
{

    public class Pacman
    {
        // Initialise variables
        public int xCoordinate = 0;
        public int yCoordinate = 0;
        private int xStart = 0;
        private int yStart = 0;
        public int currentDirection = 0;
        public int nextDirection = 0;

        public PictureBox pacman = new PictureBox();
        private ArrayList pacmanListImages = new ArrayList();

        private int imageOn = 0;

        Board gameBoard;

        public Pacman(Board board)
        {

            this.gameBoard = board;

            Add(Properties.Resources.Pacman_1_0);
            Add(Properties.Resources.Pacman_1_1);
            Add(Properties.Resources.Pacman_1_2);
            Add(Properties.Resources.Pacman_1_3);

            Add(Properties.Resources.Pacman_2_0);
            Add(Properties.Resources.Pacman_2_1);
            Add(Properties.Resources.Pacman_2_2);
            Add(Properties.Resources.Pacman_2_3);

            Add(Properties.Resources.Pacman_3_0);
            Add(Properties.Resources.Pacman_3_1);
            Add(Properties.Resources.Pacman_3_2);
            Add(Properties.Resources.Pacman_3_3);

            Add(Properties.Resources.Pacman_4_0);
            Add(Properties.Resources.Pacman_4_1);
            Add(Properties.Resources.Pacman_4_2);
            Add(Properties.Resources.Pacman_4_3);

        }

        private void Add(byte[] data)
        {
            

            var info = new SKImageInfo(14, 14); // width and height of rect

            //

            var skimage = SKBitmap.Decode(data, info);

            pacmanListImages.Add(skimage);
        }



        public void Initialize(int startXCoordinate, int startYCoordinate)
        {
            // Create Pacman Image
            xStart = startXCoordinate;
            yStart = startYCoordinate;

            SetPacman();
        }

        public void Move()
        {
            //Move(currentDirection);
            AutoMove(); 
        }

        public void Move(int direction)
        {
            // Move Pacman
            bool canMove = CheckDirection(nextDirection);

            if (!canMove)
            {
                canMove = CheckDirection(currentDirection);
                direction = currentDirection;
            }
            else
            {
                direction = nextDirection;
            }

            if (canMove)
            {
                currentDirection = direction;
            }

            if (canMove)
            {
                switch (direction)
                {
                   

                    case 1:
                        pacman.Y -= 4;

                        if ((pacman.Y % Program.BLOCK_SIZE) == 0)
                        {
                            yCoordinate--;

                            pacman.X = xCoordinate * Program.BLOCK_SIZE;
                            pacman.Y = yCoordinate * Program.BLOCK_SIZE;

                         
                        }
                        break;

                    case 2:
                        pacman.X += 4;
                 

                        if ((pacman.X % Program.BLOCK_SIZE) == 0)
                        {
                            xCoordinate++;

                            pacman.X = xCoordinate * Program.BLOCK_SIZE;
                            pacman.Y = yCoordinate * Program.BLOCK_SIZE;

                          
                        }
                        break;

                    case 3:
                        pacman.Y += 4;

                        if ((pacman.Y % Program.BLOCK_SIZE) == 0)
                        {
                            yCoordinate++;

                            pacman.X = xCoordinate * Program.BLOCK_SIZE;
                            pacman.Y = yCoordinate * Program.BLOCK_SIZE;

                           
                        }
                        break;

                    case 4:
                        pacman.X -= 4;

                      
                        if ((pacman.X % Program.BLOCK_SIZE) == 0)
                        {
                            xCoordinate--;

                            pacman.X = xCoordinate * Program.BLOCK_SIZE;
                            pacman.Y = yCoordinate * Program.BLOCK_SIZE;

                            
                        }
                        break;
                }

                pacman.Location = new Point(pacman.X, pacman.Y);

                currentDirection = direction;

                UpdatePacmanImage();
                CheckPacmanPosition();

                this.gameBoard.ghost.CheckForPacman();
            }
        }

        private void CheckPacmanPosition()
        {
            // Check Pacmans position
            switch (this.gameBoard.map[yCoordinate, xCoordinate])
            {
                case 1: this.gameBoard.food.EatSmallFood(yCoordinate, xCoordinate); break;
                case 2: this.gameBoard.food.EatBigFood(yCoordinate, xCoordinate); break;
            }
        }

        private void UpdatePacmanImage()
        {
            // Update Pacman image
            pacman.Image = pacmanListImages[((currentDirection - 1) * 4) + imageOn] as SKBitmap;
            imageOn++;
            if (imageOn > 3)
            {
                imageOn = 0;
            }
        }

        private bool CheckDirection(int direction)
        {
            // Check if pacman can move to space
            switch (direction)
            {
                case 1: return IsGoodDirection(xCoordinate, yCoordinate - 1);
                case 2: return IsGoodDirection(xCoordinate + 1, yCoordinate);
                case 3: return IsGoodDirection(xCoordinate, yCoordinate + 1);
                case 4: return IsGoodDirection(xCoordinate - 1, yCoordinate);
                default: return false;
            }
        }

        private bool IsGoodDirection(int x, int y)
        {
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

            if (this.gameBoard.map[y, x] < 4)
            {
                return true;
            }
            else
            {
                return false;
            }
        }



        public void SetPacman()
        {
            var img = Resources.Pacman_2_1;

            var info = new SKImageInfo(14, 14); // width and height of rect



            // Place Pacman in board
            pacman.Image = SKBitmap.Decode(img, info);
            currentDirection = 0;
            nextDirection = 0;

            xCoordinate = xStart;
            yCoordinate = yStart;

            pacman.Y = yStart * Program.BLOCK_SIZE;
            pacman.X = xStart * Program.BLOCK_SIZE;
            pacman.Location = new Point(pacman.X, pacman.Y);
        }

        public void AutoMove()
        {
            var canMove = false;

            while (!canMove)
            {
                canMove = CheckDirection(currentDirection);

                if (!canMove)
                {
                    currentDirection++;

                    currentDirection = currentDirection % 4;

                }
            }

            Move(currentDirection);
            //case Buttons.UP: pacman.nextDirection = 1; pacman.Move(1); break;
            //case Buttons.RIGHT: pacman.nextDirection = 2; pacman.Move(2); break;
            //case Buttons.DOWN: pacman.nextDirection = 3; pacman.Move(3); break;
            //case Buttons.LEFT: pacman.nextDirection = 4; pacman.Move(4); break;
        }
    }



}
#endif
