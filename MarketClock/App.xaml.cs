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
            Theme.Apply(AppSettings.Load().Theme);

            var window = new MainWindow();
            window.WindowStyle = WindowStyle.None;
            window.Show();
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
