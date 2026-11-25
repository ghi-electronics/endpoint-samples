using Iot.Device.Button;
using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestPacman
{
    //public class Buttons
    //{
    //    GpioController gpio_controller = new GpioController();

    //    public const int UP = 1;
    //    public const int DOWN = 2;
    //    public const int LEFT = 4;        
    //    public const int RIGHT = 8;

    //    const int LEFT_UP = 57;
    //    const int LEFT_DOWN = 59;
    //    const int LEFT_LEFT = 60;        
    //    const int LEFT_RIGHT = 58;


    //    const int RIGHT_UP = 46;
    //    const int RIGHT_DOWN = 47;
    //    const int RIGHT_LEFT = 44;        
    //    const int RIGHT_RIGHT = 64;


    //    const int SELECT = 114;
    //    const int START = 88;

    //    const int LED_1 = 89;
    //    const int LED_2 = 23;
    //    Board board;

    //    GpioButton button_leftup;
    //    GpioButton button_leftdown;
    //    GpioButton button_leftleft;
    //    GpioButton button_leftright;

    //    GpioButton button_rightup;
    //    GpioButton button_rightdown;
    //    GpioButton button_rightleft;
    //    GpioButton button_rightright;

    //    GpioButton button_select;
    //    GpioButton button_start;


    //    public Buttons(Board board)
    //    {



    //        this.board = board;
            

    //        button_leftup = new GpioButton(LEFT_UP, gpio_controller, true, PinMode.Input, TimeSpan.FromMilliseconds(10));
    //        button_leftdown = new GpioButton(LEFT_DOWN, gpio_controller, true, PinMode.Input, TimeSpan.FromMilliseconds(10));
    //        button_leftleft = new GpioButton(LEFT_LEFT, gpio_controller, true, PinMode.Input, TimeSpan.FromMilliseconds(10));
    //        button_leftright = new GpioButton(LEFT_RIGHT, gpio_controller, true, PinMode.Input, TimeSpan.FromMilliseconds(10));

    //        button_rightup = new GpioButton(RIGHT_UP, gpio_controller, true, PinMode.Input, TimeSpan.FromMilliseconds(10));
    //        button_rightdown = new GpioButton(RIGHT_DOWN, gpio_controller, true, PinMode.Input, TimeSpan.FromMilliseconds(10));
    //        button_rightleft = new GpioButton(RIGHT_LEFT, gpio_controller, true, PinMode.Input, TimeSpan.FromMilliseconds(10));
    //        button_rightright = new GpioButton(RIGHT_RIGHT, gpio_controller, true, PinMode.Input, TimeSpan.FromMilliseconds(10));

    //        button_select = new GpioButton(SELECT, gpio_controller, true, PinMode.Input, TimeSpan.FromMilliseconds(10));
    //        button_start = new GpioButton(START, gpio_controller, true, PinMode.Input, TimeSpan.FromMilliseconds(10));

    //        button_leftup.Press += Button_Pressed;
    //        button_leftdown.Press += Button_Pressed;
    //        button_leftleft.Press += Button_Pressed;
    //        button_leftright.Press += Button_Pressed;

    //        button_rightup.Press += Button_Pressed;
    //        button_rightdown.Press += Button_Pressed;
    //        button_rightleft.Press += Button_Pressed;
    //        button_rightright.Press += Button_Pressed;

    //        button_select.Press += Button_Pressed;
    //        button_start.Press += Button_Pressed;

    //        //gpio_controller.OpenPin(LED_1, PinMode.Output);
    //        //gpio_controller.OpenPin(LED_2, PinMode.Output);
    //    }

    //    private void Button_Pressed(object? sender, EventArgs e)
    //    {
    //        var s = sender as GpioButton;            

    //        if (s == button_leftup || s == button_rightup)
    //            this.board.OnKeyDown(UP);

    //        if (s == button_leftdown || s == button_rightdown)
    //            this.board.OnKeyDown(DOWN);
            
    //        if (s == button_leftleft || s == button_rightleft)
    //            this.board.OnKeyDown(LEFT);

    //        if (s == button_leftright || s == button_rightright)
    //            this.board.OnKeyDown(RIGHT);

    //        if (s == button_select || s == button_start)
    //        {
    //            //gpio_controller.Write(LED_2, gpio_controller.Read(LED_2) == PinValue.High ? PinValue.Low : PinValue.High);
    //            this.board.player.finished = true;
    //        }
           
    //    }

    //    private void PinChangeEventHandler(object sender, PinValueChangedEventArgs pinValueChangedEventArgs)
    //    {

    //        switch (pinValueChangedEventArgs.PinNumber)
    //        {
    //            case LEFT_UP:
    //            case RIGHT_UP:
    //                this.board.OnKeyDown(UP);

    //                break;

    //            case LEFT_DOWN:
    //            case RIGHT_DOWN:
    //                this.board.OnKeyDown(DOWN);
    //                break;

    //            case LEFT_LEFT:
    //            case RIGHT_LEFT:
    //                this.board.OnKeyDown(LEFT);
    //                break;

    //            case LEFT_RIGHT:
    //            case RIGHT_RIGHT:
    //                this.board.OnKeyDown(RIGHT);
    //                break;


    //        }


    //    }

        
    //}
}
