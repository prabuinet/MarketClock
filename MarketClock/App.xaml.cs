using System.Configuration;
using System.Data;
using System.Windows;

namespace MarketClock
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            RegisterTextBoxSelectAllOnFocus();

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                switch (args[1].ToLower())
                {
                    case "/c":
                        // Open config window
                        System.Windows.MessageBox.Show("No configuration yet.", "My Screensaver");
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

        /// <summary>Makes every TextBox in the app select all of its text when it gets focus.</summary>
        private static void RegisterTextBoxSelectAllOnFocus()
        {
            EventManager.RegisterClassHandler(
                typeof(System.Windows.Controls.TextBox),
                UIElement.GotKeyboardFocusEvent,
                new System.Windows.Input.KeyboardFocusChangedEventHandler((sender, _) =>
                {
                    // Multi-line boxes (the notes panel) keep the caret where it was put;
                    // selecting everything there would make the next keystroke wipe the text.
                    var textBox = (System.Windows.Controls.TextBox)sender;
                    if (!textBox.AcceptsReturn)
                    {
                        textBox.SelectAll();
                    }
                }));

            // A mouse click would otherwise place the caret right after focus and clear the selection.
            EventManager.RegisterClassHandler(
                typeof(System.Windows.Controls.TextBox),
                UIElement.PreviewMouseLeftButtonDownEvent,
                new System.Windows.Input.MouseButtonEventHandler((sender, args) =>
                {
                    var textBox = (System.Windows.Controls.TextBox)sender;
                    if (!textBox.AcceptsReturn && !textBox.IsKeyboardFocusWithin)
                    {
                        textBox.Focus();
                        args.Handled = true;
                    }
                }));
        }

    }

}
