namespace MarketClock.Panels
{
    // Clock panel: the time, with the countdown timer under it while one is running.
    // Everything shown is bound to MainWindowViewModel, which it gets from the window.
    public partial class ClockControl : System.Windows.Controls.UserControl
    {
        public ClockControl()
        {
            InitializeComponent();
        }
    }
}
