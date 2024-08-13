using Iot.Device.Buzzer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestPacman
{
    public class Sound
    {
        public Buzzer audio;


        public Sound()
        {
           // audio = new Buzzer(111);
        }
 
        public void Play(int freq, int duration)
        {
           

            //audio.StartPlaying(freq);
            //Thread.Sleep(duration);
            //audio.StopPlaying();
        }

      
    }
}
