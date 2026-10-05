using System.Windows;

namespace MarketClock
{
    // Minimize to the notification area: "Minimize" hides the window, and the icon next to
    // the Windows clock brings it back. The app keeps running (and playing) while hidden.
    public partial class MainWindow
    {
        private System.Windows.Forms.NotifyIcon? trayIcon;

        private void InitializeTrayIcon()
        {
            // Created once the window is up; a screensaver run does not get an icon.
            Loaded += (_, _) =>
            {
                if (!ScreenSaverMode)
                {
                    CreateTrayIcon();
                }
            };

            Closed += (_, _) => RemoveTrayIcon();
        }

        private void CreateTrayIcon()
        {
            if (trayIcon != null)
            {
                return;
            }

            // The app icon, at the small size the notification area uses so it stays crisp.
            // If it cannot be loaded, the standard Windows app icon is used instead.
            System.Drawing.Icon? icon = null;
            try
            {
                var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
                if (resource != null)
                {
                    using var stream = resource.Stream;
                    icon = new System.Drawing.Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
                }
            }
            catch (Exception)
            {
                // Fall through to the standard icon.
            }

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open", null, (_, _) => RestoreFromTray());
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            var play = menu.Items.Add("Play", null, (_, _) => PlaySongs());
            var pause = menu.Items.Add("Pause", null, (_, _) => PauseSongs());
            var stop = menu.Items.Add("Stop", null, (_, _) => StopSongs());
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => Close());

            // Only offer what makes sense right now.
            menu.Opening += (_, _) =>
            {
                play.Enabled = !songIsPlaying;
                pause.Enabled = songIsPlaying;
                stop.Enabled = songOutput != null;
            };

            trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = icon ?? System.Drawing.SystemIcons.Application,
                Text = "MarketClock",
                ContextMenuStrip = menu,
                Visible = true,
            };

            trayIcon.MouseClick += (_, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    RestoreFromTray();
                }
            };
        }

        private void RemoveTrayIcon()
        {
            if (trayIcon == null)
            {
                return;
            }

            // Hide it first, or Windows leaves a dead icon behind until the mouse passes over it.
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayIcon = null;
        }

        private void MinimizeMenu_Click(object sender, RoutedEventArgs e)
        {
            if (trayIcon == null)
            {
                // No icon to come back from, so use an ordinary minimize instead of vanishing.
                WindowState = WindowState.Minimized;
                return;
            }

            Hide();
        }

        private void RestoreFromTray()
        {
            Show();

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
        }
    }
}
