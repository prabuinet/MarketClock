using System.IO;
using System.Windows;
using MahApps.Metro.IconPacks;
using Controls = System.Windows.Controls;

namespace MarketClock
{
    // The column menus at the top of the window's right-click menu. There is one per column,
    // named after it and ticked while the column is shown; clicking it shows or hides the column
    // (the same as its F-key). Its sub-menu lists the panels in that column, each ticked while
    // the panel is shown. Which panels are hidden is remembered between runs; the columns'
    // names and whether they are shown are kept with the layout (see DashboardLayout).
    public partial class MainWindow
    {
        private readonly string panelsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarketClock",
            "panels.txt");

        /// <summary>What each panel is called in the menus.</summary>
        private static readonly Dictionary<string, string> PanelCaptions = new()
        {
            ["Clock"] = "Clock",
            ["HourlyBell"] = "Hourly Bell",
            ["Countdown"] = "Countdown Timer",
            ["Jee"] = "JEE Countdown",
            ["Balance"] = "Actions",
            ["NetWorth"] = "Net Worth",
            ["NetWorthGraph"] = "Net Worth Graph",
            ["Accounts"] = "Accounts",
            ["Recent"] = "Recent Transactions",
            ["Top"] = "Top Transactions This Month",
            ["DailyExpenses"] = "Daily Expenses",
            ["MonthSummary"] = "Income & Expense This Month",
            ["Songs"] = "Songs",
            ["Equalizer"] = "Equalizer",
            ["Todo"] = "To-Do List",
            ["Reminders"] = "Reminders",
            ["Notes"] = "Notes & Calculator",
            ["Habits"] = "Habit Tracker",
        };

        // The panels switched off in the menus. Anything not listed is shown, so a panel added later is shown by default.
        private readonly HashSet<string> hiddenPanels = new();

        private bool IsPanelShown(string panel) => !hiddenPanels.Contains(panel);

        /// <summary>Reads which panels are hidden and lays out the dashboard.</summary>
        private void LoadPanelLayout()
        {
            try
            {
                if (File.Exists(panelsFilePath))
                {
                    foreach (var line in File.ReadAllLines(panelsFilePath))
                    {
                        hiddenPanels.Add(line.Trim());
                    }
                }
            }
            catch (IOException)
            {
                // Unreadable file: fall back to showing every panel.
            }

            BuildDashboard();
        }

        private void SavePanelLayout()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(panelsFilePath)!);
                File.WriteAllLines(panelsFilePath, hiddenPanels.Where(dashboardPanels.ContainsKey));
            }
            catch (IOException)
            {
                // Not being able to remember the choice should not interrupt the app.
            }
        }

        // ---- The menus ----

        // The menus are made afresh each time the right-click menu opens, so they always
        // match the columns' current names and the panels currently in them.
        private void DashboardMenu_Opened(object sender, RoutedEventArgs e)
        {
            var items = DashboardMenu.Items;

            // Everything above the separator is from the last time; replace it.
            while (items.Count > 0 && !ReferenceEquals(items[0], ColumnMenusEnd))
            {
                items.RemoveAt(0);
            }

            var position = 0;

            for (var index = 0; index < dashboardLayout.Columns.Count; index++)
            {
                items.Insert(position++, CreateColumnMenu(dashboardLayout.Columns[index], index));
            }

            // The strip under the columns is not a column (no name, no F-key), but its panels
            // still need somewhere to be shown and hidden from.
            if (dashboardLayout.Bottom.Panels.Any(slot => dashboardPanels.ContainsKey(slot.Key)))
            {
                var bottom = new Controls.MenuItem { Header = "Bottom strip" };
                AddPanelMenus(bottom, dashboardLayout.Bottom);
                items.Insert(position, bottom);
            }
        }

        private Controls.MenuItem CreateColumnMenu(DashboardZone column, int index)
        {
            var menu = new Controls.MenuItem
            {
                Header = MenuCaption(column.Name),
                InputGestureText = $"F{index + 1}",
            };
            ShowColumnTick(menu, column);
            AddPanelMenus(menu, column);

            // A menu that has a sub-menu does not raise Click, so the press itself is used.
            // Presses on the sub-menu's own items pass through here too; those are not for us.
            menu.PreviewMouseLeftButtonDown += (_, e) =>
            {
                if (!ReferenceEquals(e.Source, menu))
                {
                    return;
                }

                e.Handled = true; // keeps the menu open, so several columns can be switched in one go
                ToggleColumn(index);
                ShowColumnTick(menu, column);
            };

            return menu;
        }

        /// <summary>Ticks the column's menu while the column is shown.</summary>
        private static void ShowColumnTick(Controls.MenuItem menu, DashboardZone column)
        {
            if (!column.Visible)
            {
                menu.Icon = null;
                return;
            }

            var tick = new PackIconMaterial { Kind = PackIconMaterialKind.Check, Width = 11, Height = 11 };
            tick.SetResourceReference(Controls.Control.ForegroundProperty, Theme.AccentKey);
            menu.Icon = tick;
        }

        /// <summary>Adds one tickable item per panel in the zone, in the order they are stacked.</summary>
        private void AddPanelMenus(Controls.MenuItem parent, DashboardZone zone)
        {
            foreach (var slot in zone.Panels.Where(slot => dashboardPanels.ContainsKey(slot.Key)))
            {
                var key = slot.Key;
                var item = new Controls.MenuItem
                {
                    Header = MenuCaption(PanelCaptions.GetValueOrDefault(key, key)),
                    IsCheckable = true,
                    IsChecked = IsPanelShown(key),
                    StaysOpenOnClick = true,
                };

                item.Click += (_, _) =>
                {
                    if (item.IsChecked)
                    {
                        hiddenPanels.Remove(key);
                    }
                    else
                    {
                        hiddenPanels.Add(key);
                    }

                    BuildDashboard();
                    SavePanelLayout();
                };

                parent.Items.Add(item);
            }
        }

        /// <summary>Menus read "_" as "underline the next letter"; doubling it shows a plain underscore.</summary>
        private static string MenuCaption(string text) => text.Replace("_", "__");
    }
}
