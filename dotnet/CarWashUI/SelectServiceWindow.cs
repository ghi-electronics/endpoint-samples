using GHIElectronics.Endpoint.UI.Controls;
using GHIElectronics.Endpoint.UI.Media.UIImaging;
using GHIElectronics.Endpoint.UI.Media;
using GHIElectronics.Endpoint.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using CarWashUI.Properties;
using GHIElectronics.Endpoint.Drawing;

namespace CarWashUI
{
    public sealed class SelectServiceWindow
    {
        private Canvas canvas;

        private Font fontB;
        private Font fontDroid12;
        private Font fontDroid14;
        public UIElement Elements { get; }

        public SelectServiceWindow()
        {
            this.canvas = new Canvas();
            this.fontB = new Font(16);
            this.fontDroid12 = new Font(16); ;
            this.fontDroid14 = new Font(12); ;


            this.Elements = this.CreatePage();
        }

        private UIElement CreatePage()
        {

            int xButton = 300, yButton = 15, yOffsetButton = 60;
            int xPriceText = 420, yPriceText = 25, yOffsetPriceText = 60;
            var xLine = 280;

            this.canvas.Children.Clear();

            // line
            var line = new GHIElectronics.Endpoint.UI.Shapes.Line(0, 250)
            {
                Stroke = new GHIElectronics.Endpoint.UI.Media.Pen(Colors.Yellow)
            };

            Canvas.SetLeft(line, xLine);
            Canvas.SetTop(line, 10);

            this.canvas.Children.Add(line);

            var premiumText = new GHIElectronics.Endpoint.UI.Controls.Text(this.fontDroid12, "Premium")
            {
                ForeColor = Colors.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var standardText = new GHIElectronics.Endpoint.UI.Controls.Text(this.fontDroid12, "Standard")
            {
                ForeColor = Colors.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var basicText = new GHIElectronics.Endpoint.UI.Controls.Text(this.fontDroid12, "Basic")
            {
                ForeColor = Colors.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,

            };

            var freeText = new GHIElectronics.Endpoint.UI.Controls.Text(this.fontDroid12, "Free")
            {
                ForeColor = Colors.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,

            };

            var premiumButton = new Button()
            {
                Child = premiumText,
                Width = 100,
                Height = 40,
            };

            var standardButton = new Button()
            {
                Child = standardText,
                Width = 100,
                Height = 40,
            };

            var basicButton = new Button()
            {
                Child = basicText,
                Width = 100,
                Height = 40,
            };

            var freeButton = new Button()
            {
                Child = freeText,
                Width = 100,
                Height = 40,
            };

            Canvas.SetLeft(premiumButton, xButton);
            Canvas.SetTop(premiumButton, yButton + yOffsetButton * 0);

            Canvas.SetLeft(standardButton, xButton);
            Canvas.SetTop(standardButton, yButton + yOffsetButton * 1);

            Canvas.SetLeft(basicButton, xButton);
            Canvas.SetTop(basicButton, yButton + yOffsetButton * 2);

            Canvas.SetLeft(freeButton, xButton);
            Canvas.SetTop(freeButton, yButton + yOffsetButton * 3);

            var premiumPriceText = new GHIElectronics.Endpoint.UI.Controls.Text(this.fontDroid14, "4.99$")
            {
                ForeColor = Colors.White,
            };

            var standardPriceText = new GHIElectronics.Endpoint.UI.Controls.Text(this.fontDroid14, "3.99$")
            {
                ForeColor = Colors.White,
            };

            var basicPriceText = new GHIElectronics.Endpoint.UI.Controls.Text(this.fontDroid14, "2.99$")
            {
                ForeColor = Colors.White,
            };

            var freePriceText = new GHIElectronics.Endpoint.UI.Controls.Text(this.fontDroid14, "0.00$")
            {
                ForeColor = Colors.White,
            };


            Canvas.SetLeft(premiumPriceText, xPriceText);
            Canvas.SetLeft(standardPriceText, xPriceText);
            Canvas.SetLeft(basicPriceText, xPriceText);
            Canvas.SetLeft(freePriceText, xPriceText);

            Canvas.SetTop(premiumPriceText, yPriceText + yOffsetPriceText * 0);
            Canvas.SetTop(standardPriceText, yPriceText + yOffsetPriceText * 1);
            Canvas.SetTop(basicPriceText, yPriceText + yOffsetPriceText * 2);
            Canvas.SetTop(freePriceText, yPriceText + yOffsetPriceText * 3);


            this.canvas.Children.Add(premiumButton);
            this.canvas.Children.Add(standardButton);
            this.canvas.Children.Add(basicButton);
            this.canvas.Children.Add(freeButton);

            this.canvas.Children.Add(premiumPriceText);
            this.canvas.Children.Add(standardPriceText);
            this.canvas.Children.Add(basicPriceText);
            this.canvas.Children.Add(freePriceText);



            //Timer
            var timerText = new GHIElectronics.Endpoint.UI.Controls.Text(this.fontB, DateTime.Now.Day + " / " + DateTime.Now.Month + " / " + DateTime.Now.Year)
            {
                ForeColor = Colors.White,
            };

            Canvas.SetLeft(timerText, 100);
            Canvas.SetTop(timerText, 250);

            this.canvas.Children.Add(timerText);

            var selectVehicleText = new GHIElectronics.Endpoint.UI.Controls.Text(this.fontB, "Select Vehicle Type:")
            {
                ForeColor = Colors.White,
            };


            Canvas.SetLeft(selectVehicleText, 10);
            Canvas.SetTop(selectVehicleText, yPriceText);

            this.canvas.Children.Add(selectVehicleText);

            var listBox = new ListBox();

            listBox.Items.Add(new ListBoxItemHighlightable("Truck", this.fontB, 4, GHIElectronics.Endpoint.UI.Media.Colors.Blue, GHIElectronics.Endpoint.UI.Media.Colors.White, GHIElectronics.Endpoint.UI.Media.Colors.White));
            listBox.Items.Add(new ListBoxItemHighlightable("Van", this.fontB, 4, GHIElectronics.Endpoint.UI.Media.Colors.Blue, GHIElectronics.Endpoint.UI.Media.Colors.White, GHIElectronics.Endpoint.UI.Media.Colors.White));
            listBox.Items.Add(new ListBoxItemHighlightable("SUV", this.fontB, 4, GHIElectronics.Endpoint.UI.Media.Colors.Blue, GHIElectronics.Endpoint.UI.Media.Colors.White, GHIElectronics.Endpoint.UI.Media.Colors.White));
            listBox.Items.Add(new ListBoxItemHighlightable("Sedan", this.fontB, 4, GHIElectronics.Endpoint.UI.Media.Colors.Blue, GHIElectronics.Endpoint.UI.Media.Colors.White, GHIElectronics.Endpoint.UI.Media.Colors.White));
            listBox.Items.Add(new ListBoxItemHighlightable("Other", this.fontB, 4, GHIElectronics.Endpoint.UI.Media.Colors.Blue, GHIElectronics.Endpoint.UI.Media.Colors.White, GHIElectronics.Endpoint.UI.Media.Colors.White));

            listBox.SelectedIndex = 0;

            Canvas.SetLeft(listBox, 160);
            Canvas.SetTop(listBox, yPriceText);

            this.canvas.Children.Add(listBox);


            //var dropDown = new Dropdown()
            //{
            //    Width = 100,
            //    Height = 20,
            //    Font = fontB
            //};

            //var option = new ArrayList();

            //option.Add("text 1");
            //option.Add("text 2");
            //option.Add("text 3");
            //option.Add("text 4");
            //option.Add("text 5");
            //option.Add("text 6");
            //option.Add("text 7");

            //dropDown.Options = option;

            //Canvas.SetLeft(dropDown, 160);
            //Canvas.SetTop(dropDown, yPriceText);

            //this.canvas.Children.Add(dropDown);

            var imageVisa = new GHIElectronics.Endpoint.UI.Controls.Image()
            {
                Source = UIBitmap.FromData(Resources.visa)
            };

            var imageMaster = new GHIElectronics.Endpoint.UI.Controls.Image()
            {
                Source = UIBitmap.FromData(Resources.master)
            };

            var imagePaypal = new GHIElectronics.Endpoint.UI.Controls.Image()
            {
                Source = UIBitmap.FromData(Resources.paypal)
            };

            Canvas.SetLeft(imageVisa, 20);
            Canvas.SetTop(imageVisa, yPriceText + 150);

            this.canvas.Children.Add(imageVisa);

            Canvas.SetLeft(imageMaster, 100);
            Canvas.SetTop(imageMaster, yPriceText + 150);

            this.canvas.Children.Add(imageMaster);

            Canvas.SetLeft(imagePaypal, 180);
            Canvas.SetTop(imagePaypal, yPriceText + 150);

            this.canvas.Children.Add(imagePaypal);


            // Add event
            premiumButton.Click += this.Button_Click;
            standardButton.Click += this.Button_Click;
            basicButton.Click += this.Button_Click;
            freeButton.Click += this.Button_Click;

            return this.canvas;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Program.WpfWindow.Child = Program.PaymentePage.Elements;

            //Debug.WriteLine("Button click");

            Program.WpfWindow.Invalidate();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            // catch listbox changed
        }
    }
}
