using System.Globalization;
using System.Windows;
using MarketClock.Data;

namespace MarketClock
{
    // Moves an amount from one account to another. It is saved as a pair of transactions,
    // money out of one account and into the other, which the dashboard does not count
    // as an expense or an income.
    public partial class TransferWindow : System.Windows.Window
    {
        private AccountStore? accountStore;

        public TransferWindow()
        {
            InitializeComponent();
            TransferDatePicker.SelectedDate = DateTime.Today;
            Loaded += TransferWindow_Loaded;
        }

        private async void TransferWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Opening the database is slow the first time, so keep it off the UI thread.
                var (store, accounts) = await Task.Run(() =>
                {
                    var newStore = new AccountStore();
                    return (newStore, newStore.GetAccounts());
                });

                accountStore = store;

                FromComboBox.ItemsSource = accounts;
                ToComboBox.ItemsSource = accounts;

                if (accounts.Count < 2)
                {
                    MessageTextBlock.Text = "A transfer needs two accounts. Create them first.";
                    return;
                }

                FromComboBox.SelectedIndex = 0;
                ToComboBox.SelectedIndex = 1;

                MessageTextBlock.Text = "";
                SaveButton.IsEnabled = true;
                AmountTextBox.Focus();
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not load accounts: {ex.Message}";
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (accountStore == null
                || FromComboBox.SelectedItem is not Account from
                || ToComboBox.SelectedItem is not Account to)
            {
                MessageTextBlock.Text = "Select both accounts.";
                return;
            }

            if (from.Id == to.Id)
            {
                MessageTextBlock.Text = "Choose two different accounts.";
                return;
            }

            if (!TryParseAmount(AmountTextBox.Text, out var amount) || amount <= 0)
            {
                MessageTextBlock.Text = "Enter an amount greater than zero.";
                return;
            }

            var note = NoteTextBox.Text.Trim();
            var date = TransferDatePicker.SelectedDate ?? DateTime.Today;
            var store = accountStore;

            SaveButton.IsEnabled = false;
            try
            {
                await Task.Run(() => store.Transfer(from.Id, to.Id, date, amount, note));
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not save the transfer: {ex.Message}";
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
