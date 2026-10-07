using System.Windows;
using System.Windows.Controls;
using MarketClock.Data;

namespace MarketClock
{
    public partial class SettingsWindow : System.Windows.Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            var settings = AppSettings.Load();
            SongsFolderTextBox.Text = settings.SongsFolder;
            HourlyBellStartTextBox.Text = AppSettings.FormatTimeOfDay(
                AppSettings.ParseTimeOfDay(settings.HourlyBellStart) ?? TimeSpan.FromHours(7));
            HourlyBellEndTextBox.Text = AppSettings.FormatTimeOfDay(
                AppSettings.ParseTimeOfDay(settings.HourlyBellEnd) ?? TimeSpan.FromHours(23));

            SettingsTree.SelectedItemChanged += SettingsTree_SelectedItemChanged;
            AccountCategoriesItem.IsSelected = true;
            Loaded += SettingsWindow_Loaded;
        }

        private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Opening the database is slow the first time, so keep it off the UI thread.
                var store = await Task.Run(() => new AccountStore());

                AccountCategoriesEditor.Initialize(
                    store.GetAccountCategories,
                    name => store.AddAccountCategory(name),
                    store.RenameAccountCategory);

                ExpenseCategoriesEditor.Initialize(
                    store.GetExpenseCategories,
                    name => store.AddExpenseCategory(name),
                    store.RenameExpenseCategory);

                SettingsMessage.Text = "";
            }
            catch (Exception ex)
            {
                SettingsMessage.Text = $"Could not load settings: {ex.Message}";
            }
        }

        private void SettingsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Each tree node names its page through its Tag.
            var page = (e.NewValue as TreeViewItem)?.Tag as string;

            AccountsPage.Visibility = ToVisibility(page == "Accounts");
            AccountCategoriesPage.Visibility = ToVisibility(page == "AccountCategories");
            ExpenseCategoriesPage.Visibility = ToVisibility(page == "ExpenseCategories");
            MediaPage.Visibility = ToVisibility(page == "Media");
            SongsPage.Visibility = ToVisibility(page == "Songs");
            HourlyBellPage.Visibility = ToVisibility(page == "HourlyBell");
        }

        private void BrowseSongsFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choose the songs folder",
            };

            var current = SongsFolderTextBox.Text.Trim();
            if (System.IO.Directory.Exists(current))
            {
                dialog.InitialDirectory = current;
            }

            if (dialog.ShowDialog(this) == true)
            {
                SongsFolderTextBox.Text = dialog.FolderName;
            }
        }

        private void SaveSongsFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = SongsFolderTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder))
            {
                SongsFolderMessage.Text = "Enter a folder path.";
                return;
            }

            try
            {
                // Load first, so saving this page does not reset any other setting.
                var settings = AppSettings.Load();
                settings.SongsFolder = folder;
                settings.Save();
            }
            catch (Exception ex)
            {
                SongsFolderMessage.Text = $"Could not save: {ex.Message}";
                return;
            }

            SongsFolderMessage.Text = System.IO.Directory.Exists(folder)
                ? "Saved."
                : "Saved, but that folder does not exist yet.";

            // Show the new folder on the dashboard straight away.
            (Owner as MainWindow)?.LoadSongs();
        }

        private void SaveHourlyBell_Click(object sender, RoutedEventArgs e)
        {
            var start = AppSettings.ParseTimeOfDay(HourlyBellStartTextBox.Text.Trim());
            var end = AppSettings.ParseTimeOfDay(HourlyBellEndTextBox.Text.Trim());

            if (start == null || end == null)
            {
                HourlyBellMessage.Text = "Enter both times like 7:00 AM or 23:00.";
                return;
            }

            try
            {
                // Load first, so saving this page does not reset any other setting.
                var settings = AppSettings.Load();
                settings.HourlyBellStart = start.Value.ToString(@"hh\:mm");
                settings.HourlyBellEnd = end.Value.ToString(@"hh\:mm");
                settings.Save();
            }
            catch (Exception ex)
            {
                HourlyBellMessage.Text = $"Could not save: {ex.Message}";
                return;
            }

            // Show the times back the way they were understood.
            HourlyBellStartTextBox.Text = AppSettings.FormatTimeOfDay(start.Value);
            HourlyBellEndTextBox.Text = AppSettings.FormatTimeOfDay(end.Value);
            HourlyBellMessage.Text = "Saved.";

            // Use the new hours on the dashboard straight away.
            (Owner as MainWindow)?.LoadHourlyBellSettings();
        }

        private static Visibility ToVisibility(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
