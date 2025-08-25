using System.Configuration;
using System.Data;
using System.Windows;

namespace MarketClock
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                switch (args[1].ToLower())
                {
                    case "/c":
                        // Open config window
                        MessageBox.Show("No configuration yet.", "My Screensaver");
                        Shutdown();
                        return;

                    case "/p":
                        // Preview inside Control Panel (not trivial in WPF, can ignore or stub)
                        Shutdown();
                        return;

                    case "/s":
                        // Fullscreen mode
                        var win = new MainWindow();
                        win.WindowState = WindowState.Maximized;
                        win.WindowStyle = WindowStyle.None;
                        win.Topmost = true;
                        win.ScreenSaverMode = true;
                        win.Show();
                        return;
                }
            }

            // Default to screensaver mode
            var defaultWin = new MainWindow();
            // defaultWin.WindowState = WindowState.Maximized;
            defaultWin.WindowStyle = WindowStyle.None;
            defaultWin.Topmost = true;
            defaultWin.Show();
        }

    }

}
