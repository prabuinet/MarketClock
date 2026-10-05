using System.Globalization;
using System.Windows;
using MarketClock.Data;

namespace MarketClock
{
    public partial class ExpenseWindow : System.Windows.Window
    {
        private AccountStore? accountStore;

        public ExpenseWindow()
        {
            InitializeComponent();
            ExpenseDatePicker.SelectedDate = DateTime.Today;
            Loaded += ExpenseWindow_Loaded;
        }

        private async void ExpenseWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Opening the database is slow the first time, so keep it off the UI thread.
                var (store, accounts, categories) = await Task.Run(() =>
                {
                    var newStore = new AccountStore();
                    return (newStore, newStore.GetAccounts(), newStore.GetExpenseCategories());
                });

                accountStore = store;

                // Id 0 is the "no category" choice; it is saved as no category at all.
                categories.Insert(0, new ExpenseCategory { Id = 0, Name = "(none)" });
                CategoryComboBox.ItemsSource = categories;
                CategoryComboBox.SelectedIndex = 0;

                AccountComboBox.ItemsSource = accounts;
                AccountComboBox.SelectedIndex = accounts.Count > 0 ? 0 : -1;

                if (accounts.Count == 0)
                {
                    MessageTextBlock.Text = "Create an account first.";
                    return;
                }

                MessageTextBlock.Text = "";
                SaveButton.IsEnabled = true;
                DescriptionTextBox.Focus();
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not load accounts: {ex.Message}";
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (accountStore == null || AccountComboBox.SelectedItem is not Account account)
            {
                MessageTextBlock.Text = "Select an account.";
                return;
            }

            // Description is optional.
            var description = DescriptionTextBox.Text.Trim();

            if (!TryParseAmount(AmountTextBox.Text, out var amount) || amount <= 0)
            {
                MessageTextBlock.Text = "Enter an amount greater than zero.";
                return;
            }

            int? categoryId = CategoryComboBox.SelectedItem is ExpenseCategory { Id: > 0 } category
                ? category.Id
                : null;

            var date = ExpenseDatePicker.SelectedDate ?? DateTime.Today;
            var store = accountStore;

            SaveButton.IsEnabled = false;
            try
            {
                // An expense reduces the balance, so it is stored as a negative amount.
                await Task.Run(() => store.AddTransaction(account.Id, date, description, -amount, categoryId));
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not save the expense: {ex.Message}";
                SaveButton.IsEnabled = true;
            }
        }

        private static bool TryParseAmount(string text, out decimal amount)
        {
            return decimal.TryParse(text, NumberStyles.Currency, CultureInfo.CurrentCulture, out amount) ||
                   decimal.TryParse(text, NumberStyles.Currency, CultureInfo.InvariantCulture, out amount);
        }
    }
}
