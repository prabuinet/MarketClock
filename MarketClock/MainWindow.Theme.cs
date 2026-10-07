using System.Windows;
using Controls = System.Windows.Controls;
using Media = System.Windows.Media;

namespace MarketClock
{
    // The Theme entry of the right-click menu: one line per theme (see Theme.cs), each with a
    // dot of its colour, and a tick on the one in use.
    public partial class MainWindow
    {
        private void InitializeThemeMenu()
        {
            foreach (var theme in Theme.All)
            {
                var caption = new Controls.StackPanel { Orientation = Controls.Orientation.Horizontal };
                caption.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = new Media.SolidColorBrush(theme.Accent),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                });
                caption.Children.Add(new Controls.TextBlock { Text = theme.Name, VerticalAlignment = System.Windows.VerticalAlignment.Center });

                var item = new Controls.MenuItem
                {
                    Header = caption,
                    Tag = theme.Name,
                    IsCheckable = true,
                    IsChecked = theme == Theme.Current,
                    StaysOpenOnClick = true, // so themes can be tried one after another
                };
                item.Click += ThemeMenuItem_Click;
                ThemeMenu.Items.Add(item);
            }
        }

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not string name)
            {
                return;
            }

            Theme.Apply(name);

            // Exactly one theme is ticked, including when the current one is clicked again.
            foreach (var item in ThemeMenu.Items.OfType<Controls.MenuItem>())
            {
                item.IsChecked = (item.Tag as string) == Theme.Current.Name;
            }

            try
            {
                var settings = AppSettings.Load();
                settings.Theme = Theme.Current.Name;
                settings.Save();
            }
            catch (Exception)
            {
                // Not being able to remember the theme should not interrupt anything.
            }
        }
    }
}
