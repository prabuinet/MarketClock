using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Controls = System.Windows.Controls;
using Media = System.Windows.Media;

namespace MarketClock.Panels
{
    // Clock panel: the time, with the countdown timer under it while one is running.
    // The time comes in more than one look (digital, analog); clicking the clock moves on to
    // the next, and the choice is remembered. The digital clock and the timer are bound to
    // MainWindowViewModel, which the panel gets from the window; the analog clock is drawn here.
    public partial class ClockControl : System.Windows.Controls.UserControl
    {
        // The looks, in the order a click goes through them. To add one, add its view here.
        private readonly FrameworkElement[] clockViews;

        private readonly DispatcherTimer analogTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
        private int clockStyle;

        public ClockControl()
        {
            InitializeComponent();
            clockViews = new FrameworkElement[] { DigitalClock, AnalogClock };

            DrawAnalogFace();
            analogTimer.Tick += (_, _) => UpdateAnalogClock();

            // The hands only need to move while the panel is on show.
            Loaded += (_, _) => ShowClockStyle(clockStyle);
            Unloaded += (_, _) => analogTimer.Stop();
        }

        /// <summary>Restores the look that was last chosen.</summary>
        public void LoadSettings()
        {
            ShowClockStyle(AppSettings.Load().ClockStyle);
        }

        private void ShowClockStyle(int style)
        {
            clockStyle = ((style % clockViews.Length) + clockViews.Length) % clockViews.Length;

            for (var i = 0; i < clockViews.Length; i++)
            {
                clockViews[i].Visibility = i == clockStyle ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ReferenceEquals(clockViews[clockStyle], AnalogClock) && IsLoaded)
            {
                UpdateAnalogClock();
                analogTimer.Start();
            }
            else
            {
                analogTimer.Stop();
            }
        }

        // ---- Clicking the clock ----

        private void Clock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // The same press also drags the window (the window handles it after this), so it is
            // not marked as handled. Once the button is let go: if the window has not moved,
            // it was a click, and the clock changes.
            var window = Window.GetWindow(this);
            if (window == null)
            {
                return;
            }

            var left = window.Left;
            var top = window.Top;

            var released = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            released.Tick += (_, _) =>
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    return;
                }

                released.Stop();

                if (window.Left == left && window.Top == top)
                {
                    ShowClockStyle(clockStyle + 1);
                    SaveClockStyle();
                }
            };
            released.Start();
        }

        private void SaveClockStyle()
        {
            try
            {
                var settings = AppSettings.Load();
                settings.ClockStyle = clockStyle;
                settings.Save();
            }
            catch (Exception)
            {
                // Not being able to remember the look should not interrupt anything.
            }
        }

        // ---- Analog clock ----
        // Drawn on a 200 x 200 canvas (scaled to the panel by its Viewbox), centre at 100,100.

        /// <summary>Adds the minute marks and the hour numbers round the face.</summary>
        private void DrawAnalogFace()
        {
            var faint = new Media.SolidColorBrush(Media.Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));
            var font = (Media.FontFamily)FindResource("JetBrainsMonoRegular");

            for (var minute = 0; minute < 60; minute++)
            {
                var isHour = minute % 5 == 0;
                var angle = minute * 6 * Math.PI / 180;
                var (sin, cos) = (Math.Sin(angle), Math.Cos(angle));
                var inner = isHour ? 88 : 93;

                AnalogFace.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = 100 + inner * sin,
                    Y1 = 100 - inner * cos,
                    X2 = 100 + 97 * sin,
                    Y2 = 100 - 97 * cos,
                    Stroke = isHour ? Media.Brushes.White : faint,
                    StrokeThickness = isHour ? 2 : 1,
                });

                if (isHour)
                {
                    // A 24 x 16 box centred on the spot, so the number is centred whatever its width.
                    var number = new Controls.TextBlock
                    {
                        Text = (minute == 0 ? 12 : minute / 5).ToString(),
                        Width = 24,
                        Height = 16,
                        TextAlignment = TextAlignment.Center,
                        FontFamily = font,
                        FontSize = 12,
                        Foreground = Media.Brushes.White,
                    };
                    Controls.Canvas.SetLeft(number, 100 + 76 * sin - 12);
                    Controls.Canvas.SetTop(number, 100 - 76 * cos - 8);
                    AnalogFace.Children.Add(number);
                }
            }
        }

        private void UpdateAnalogClock()
        {
            var now = DateTime.Now;

            HourRotation.Angle = (now.Hour % 12 + now.Minute / 60.0) * 30;
            MinuteRotation.Angle = (now.Minute + now.Second / 60.0) * 6;
            SecondRotation.Angle = now.Second * 6;

            AnalogDayText.Text = now.ToString("dddd").ToUpper();
            AnalogDayNumberText.Text = now.ToString("dd");
            AnalogMonthText.Text = now.ToString("MMM yyyy").ToUpper();
        }
    }
}
