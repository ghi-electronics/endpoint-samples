using GHIElectronics.Endpoint.UI.Controls;
using GHIElectronics.Endpoint.UI.Media;
using GHIElectronics.Endpoint.UI.Threading;
using GHIElectronics.Endpoint.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GHIElectronics.Endpoint.Drawing;

namespace CarWashUI
{
    internal class LoadingPage
    {
        private Canvas canvas;
        private Font fontB;
        private DispatcherTimer timer;
        private ProgressBar progressBar;

        public UIElement Elements { get; }

        public LoadingPage()
        {
            this.canvas = new Canvas();
            this.fontB = new Font(16);

            this.progressBar = new ProgressBar()
            {
                MaxValue = 100,
                Value = 0,
                Width = 200,
                Height = 20
            };

            this.timer = new DispatcherTimer
            {
                Tag = this.progressBar
            };
            this.timer.Tick += this.Counter;
            this.timer.Interval = new TimeSpan(0, 0, 1);

            this.Elements = this.CreatePage();
        }
        public void Active() => this.timer.Start();

        public void Deactive() => this.timer.Stop();

        private UIElement CreatePage()
        {
            this.canvas.Children.Clear();

            var loadingText = new GHIElectronics.Endpoint.UI.Controls.Text(this.fontB, "Processing your payment...")
            {
                ForeColor = Colors.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            Canvas.SetLeft(loadingText, 140);
            Canvas.SetTop(loadingText, 220);

            this.canvas.Children.Add(loadingText);

            Canvas.SetLeft(this.progressBar, 140);
            Canvas.SetTop(this.progressBar, 240);

            this.canvas.Children.Add(this.progressBar);

            return this.canvas;

        }

        void Counter(object sender, EventArgs e)
        {
            this.progressBar.Value += 10;
            this.progressBar.Invalidate();

            if (this.progressBar.Value == this.progressBar.MaxValue)
            {
                this.timer.Stop();

                this.progressBar.Value = 0;

                Program.WpfWindow.Child = Program.CarWashPage.Elements;
                Program.WpfWindow.Invalidate();

                Program.CarWashPage.Active();
            }

        }
    }
}
