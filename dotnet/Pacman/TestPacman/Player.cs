using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestPacman
{
    public class Player
    {


        public bool finished = false;
        public bool won = false;


        Board gameboard;
        public Player(Board gameboard)
        {
            this.gameboard = gameboard;
        }
        public void Lose()
        {
            // Nerver lose :))
            this.gameboard.pacman.SetPacman();
            this.gameboard.ghost.ResetGhosts();
        
        }

        public void Won()
        {
            
            finished = true;
            won = true;
        }
    }
}
