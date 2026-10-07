using System.Globalization;
using System.Windows;
using MarketClock.Data;

namespace MarketClock
{
    // Adds an expense or, when opened with income: true, an income. The two differ only in
    // wording, in the sign the amount is saved with, and in income having no category.
    // Opened with a transaction id it edits (or deletes) that saved transaction instead,
    // whatever kind it is.
    public partial class ExpenseWindow : System.Windows.Window
    {
        private enum Kind
        {
            Expense,
            Income,
            Transfer,
            DailyMtm,
            OpeningBalance,
        }

        private readonly int? editingId;
        private Kind kind;
        private AccountStore? accountStore;

        // The saved transaction being edited and its account; null while adding a new one.
        private AccountTransaction? editing;
        private Account? editingAccount;

        public ExpenseWindow(bool income = false)
        {
            InitializeComponent();
            kind = income ? Kind.Income : Kind.Expense;

            if (income)
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

        /// <summary>Opens the form to edit the saved transaction with this id.</summary>
        public ExpenseWindow(int transactionId)
            : this()
        {
            editingId = transactionId;
            Title = "Edit Transaction";
            HeadingText.Text = "Edit Transaction";
            SaveButton.Content = "Save";
        }

        /// <summary>The amount box takes a signed number (a loss or a loan is negative) rather than one above zero.</summary>
        private bool AmountIsSigned => kind is Kind.DailyMtm or Kind.OpeningBalance;

        private string KindName => kind switch
        {
            Kind.Income => "income",
            Kind.Transfer => "transfer",
            Kind.DailyMtm => "daily MTM",
            Kind.OpeningBalance => "opening balance",
            _ => "expense",
        };

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

                if (editingId != null && !ShowTransactionToEdit(accounts, categories))
                {
                    MessageTextBlock.Text = "That transaction no longer exists.";
                    return;
                }

                SaveButton.IsEnabled = true;
                DescriptionTextBox.Focus();
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not load accounts: {ex.Message}";
            }
        }

        /// <summary>Fills the form with the transaction being edited. False when it cannot be found.</summary>
        private bool ShowTransactionToEdit(List<Account> accounts, List<ExpenseCategory> categories)
        {
            editingAccount = accounts.FirstOrDefault(a => a.Transactions.Any(t => t.Id == editingId));
            editing = editingAccount?.Transactions.First(t => t.Id == editingId);

            if (editingAccount == null || editing == null)
            {
                return false;
            }

            // An account's oldest row is its opening balance.
            var isOpeningBalance = editingAccount.Transactions.Min(t => t.Id) == editing.Id;

            kind = isOpeningBalance ? Kind.OpeningBalance
                : editing.IsTransfer ? Kind.Transfer
                : editing.IsDailyMtm ? Kind.DailyMtm
                : editing.Amount > 0 ? Kind.Income
                : Kind.Expense;

            var heading = kind switch
            {
                Kind.Income => "Edit Income",
                Kind.Transfer => "Edit Transfer",
                Kind.DailyMtm => "Edit Daily MTM",
                Kind.OpeningBalance => "Edit Opening Balance",
                _ => "Edit Expense",
            };

            Title = heading;
            HeadingText.Text = heading;

            // Only an ordinary expense or income can be moved to another account.
            AccountComboBox.SelectedItem = editingAccount;
            AccountComboBox.IsEnabled = kind is Kind.Expense or Kind.Income;

            var showCategory = kind == Kind.Expense ? Visibility.Visible : Visibility.Collapsed;
            CategoryLabel.Visibility = showCategory;
            CategoryComboBox.Visibility = showCategory;
            CategoryComboBox.SelectedItem = categories.FirstOrDefault(c => c.Id == (editing.ExpenseCategoryId ?? 0)) ?? categories[0];

            // There is one Daily MTM per account per day; its day is changed on the Daily MTM form.
            ExpenseDatePicker.SelectedDate = editing.Date;
            ExpenseDatePicker.IsEnabled = kind != Kind.DailyMtm;

            DescriptionTextBox.Text = editing.Description;

            AmountLabel.Text = kind switch
            {
                Kind.DailyMtm => "Amount (negative for a loss)",
                Kind.OpeningBalance => "Amount (negative for a loan)",
                _ => "Amount",
            };
            AmountTextBox.Text = (AmountIsSigned ? editing.Amount : Math.Abs(editing.Amount)).ToString("0.00");

            if (kind == Kind.Transfer)
            {
                MessageTextBlock.Text = "The date and the amount change in both accounts of the transfer.";
            }

            // An opening balance stays for as long as its account does.
            DeleteButton.Visibility = isOpeningBalance ? Visibility.Collapsed : Visibility.Visible;
            return true;
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

            if (!TryParseAmount(AmountTextBox.Text, out var amount))
            {
                MessageTextBlock.Text = "Enter a valid amount.";
                return;
            }

            if (!AmountIsSigned && amount <= 0)
            {
                MessageTextBlock.Text = "Enter an amount greater than zero.";
                return;
            }

            // Categories are expense categories, so anything else is saved without one.
            int? categoryId = kind == Kind.Expense && CategoryComboBox.SelectedItem is ExpenseCategory { Id: > 0 } category
                ? category.Id
                : null;

            // An expense reduces the balance, so it is stored as a negative amount; an income as a positive one.
            // A transfer stays on the side it was on: money out of this account, or money into it.
            var signedAmount = kind switch
            {
                Kind.Expense => -amount,
                Kind.Transfer when editing is { Amount: < 0 } => -amount,
                _ => amount,
            };

            var date = ExpenseDatePicker.SelectedDate ?? DateTime.Today;
            var store = accountStore;
            var transactionId = editing?.Id;

            SaveButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
            try
            {
                await Task.Run(() =>
                {
                    if (transactionId == null)
                    {
                        store.AddTransaction(account.Id, date, description, signedAmount, categoryId);
                    }
                    else
                    {
                        store.UpdateTransaction(transactionId.Value, account.Id, date, description, signedAmount, categoryId);
                    }
                });
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageTextBlock.Text = $"Could not save the {KindName}: {ex.Message}";
                SaveButton.IsEnabled = true;
                DeleteButton.IsEnabled = true;
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (editing == null || editingAccount == null)
            {
                return;
            }

            var summary = $"{editing.Date:dd MMM yyyy} · {editing.Description} · {editingAccount.Name} · {editing.Amount:N2}";

            if (TransactionActions.Delete(this, editing.Id, summary, editing.IsTransfer))
            {
                DialogResult = true;
            }
        }

        private static bool TryParseAmount(string text, out decimal amount)
        {
            return decimal.TryParse(text, NumberStyles.Currency, CultureInfo.CurrentCulture, out amount) ||
                   decimal.TryParse(text, NumberStyles.Currency, CultureInfo.InvariantCulture, out amount);
        }
    }
}
