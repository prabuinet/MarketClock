using System.Globalization;
using System.Windows;
using MarketClock.Data;

namespace MarketClock
{
    // Adds an expense or, when opened with income: true, an income. The two differ only in
    // wording, in the sign the amount is saved with, and in income having no category.
    public partial class ExpenseWindow : System.Windows.Window
    {
        private readonly bool isIncome;
        private AccountStore? accountStore;

        public ExpenseWindow(bool income = false)
        {
            InitializeComponent();
            isIncome = income;

            if (isIncome)
            {
                Title = "Add Income";
                HeadingText.Text = "Add Income";
                SaveButton.Content = "+ Add Income";
                CategoryLabel.Visibility = Visibility.Collapsed;
                CategoryComboBox.Visibility = Visibility.Collapsed;
            }

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

            // Categories are expense categories, so an income is saved without one.
            int? categoryId = !isIncome && CategoryComboBox.SelectedItem is ExpenseCategory { Id: > 0 } category
                ? category.Id
                : null;

            var date = ExpenseDatePicker.SelectedDate ?? DateTime.Today;
            var store = accountStore;

            SaveButton.IsEnabled = false;
            try
            {
                // An expense reduces the balance, so it is stored as a negative amount; an income as a positive one.
                var signedAmount = isIncome ? amount : -amount;
                await Task.Run(() => store.AddTransaction(account.Id, date, description, signedAmount, categoryId));
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not save the {(isIncome ? "income" : "expense")}: {ex.Message}";
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
