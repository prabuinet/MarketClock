using System.IO;
using System.Windows;

namespace MarketClock
{
    // Dashboard panels: which ones are shown is chosen from the "Panels" context menu
    // and remembered between runs.
    public partial class MainWindow
    {
        private readonly string panelsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarketClock",
            "panels.txt");

        private IEnumerable<System.Windows.Controls.MenuItem> PanelMenuItems =>
            PanelsMenu.Items.OfType<System.Windows.Controls.MenuItem>();

        private bool IsPanelShown(string panel) =>
            PanelMenuItems.Any(item => (item.Tag as string) == panel && item.IsChecked);

        /// <summary>Ticks the menu items to match the saved choice and lays out the dashboard.</summary>
        private void LoadPanelLayout()
        {
            try
            {
                if (File.Exists(panelsFilePath))
                {
                    // The file lists the hidden panels, so a panel added later is shown by default.
                    var hidden = File.ReadAllLines(panelsFilePath).Select(line => line.Trim()).ToHashSet();

                    foreach (var item in PanelMenuItems)
                    {
                        item.IsChecked = !hidden.Contains(item.Tag as string ?? "");
                    }
                }
            }
            catch (IOException)
            {
                // Unreadable file: fall back to showing every panel.
            }

            ApplyPanelLayout();
        }

        private void SavePanelLayout()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(panelsFilePath)!);

                var hidden = PanelMenuItems
                    .Where(item => !item.IsChecked)
                    .Select(item => item.Tag as string ?? "");
                File.WriteAllLines(panelsFilePath, hidden);
            }
            catch (IOException)
            {
                // Not being able to remember the layout should not interrupt the app.
            }
        }

        private void PanelMenu_Click(object sender, RoutedEventArgs e)
        {
            ApplyPanelLayout();
            SavePanelLayout();
        }

        /// <summary>Shows the ticked panels and hides the rest (the layout itself lives in MainWindow.Layout.cs).</summary>
        private void ApplyPanelLayout()
        {
            BuildDashboard();
        }
    }
}
