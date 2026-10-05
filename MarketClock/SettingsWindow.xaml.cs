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

            SongsFolderTextBox.Text = AppSettings.Load().SongsFolder;

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

        private static Visibility ToVisibility(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
