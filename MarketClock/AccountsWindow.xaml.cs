using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using MarketClock.Data;

namespace MarketClock
{
    public partial class AccountsWindow : System.Windows.Window, INotifyPropertyChanged
    {
        private AccountStore? accountStore;
        private Account? selectedAccount;
        private Account? editingAccount;

        public AccountsWindow()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += AccountsWindow_Loaded;
        }

        private async void AccountsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;

            try
            {
                // Opening the database and the first query are slow, so keep them off the UI thread.
                var (store, accounts) = await Task.Run(() =>
                {
                    var newStore = new AccountStore();
                    return (newStore, newStore.GetAccounts());
                });

                accountStore = store;

                foreach (var account in accounts)
                {
                    Accounts.Add(account);
                }

                SelectedAccount = Accounts.FirstOrDefault();
            }
            catch (Exception ex)
            {
                FormMessageTextBlock.Text = $"Could not load accounts: {ex.Message}";
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<Account> Accounts { get; } = new();

        public Account? SelectedAccount
        {
            get => selectedAccount;
            set
            {
                if (selectedAccount == value)
                {
                    return;
                }

                selectedAccount = value;
                OnPropertyChanged();
            }
        }

        private void CreateAccount_Click(object sender, RoutedEventArgs e)
        {
            if (accountStore == null)
            {
                FormMessageTextBlock.Text = "Accounts are not loaded.";
                return;
            }

            var name = AccountNameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                FormMessageTextBlock.Text = "Enter an account name.";
                return;
            }

            var category = CategoryComboBox.SelectedItem as AccountCategory;

            if (editingAccount != null)
            {
                SaveEditedAccount(editingAccount, name, category);
                return;
            }

            if (!TryParseAmount(OpeningBalanceTextBox.Text, out var openingBalance))
            {
                FormMessageTextBlock.Text = "Enter a valid opening balance.";
                return;
            }

            var account = accountStore.CreateAccount(name, category?.Id, openingBalance);
            Accounts.Add(account);
            SelectedAccount = account;

            ShowCreateAccountForm(false);
        }

        private void SaveEditedAccount(Account account, string name, AccountCategory? category)
        {
            accountStore!.UpdateAccount(account.Id, name, category?.Id);

            account.Name = name;
            account.CategoryId = category?.Id;
            account.Category = category;

            // Re-insert the account so the list row and the header pick up the new values.
            var index = Accounts.IndexOf(account);
            Accounts.RemoveAt(index);
            Accounts.Insert(index, account);
            SelectedAccount = account;

            ShowCreateAccountForm(false);
        }

        private void AddAccount_Click(object sender, RoutedEventArgs e)
        {
            if (accountStore == null)
            {
                FormMessageTextBlock.Text = "Accounts are not loaded.";
                return;
            }

            ShowCreateAccountForm(true);
        }

        private void EditAccount_Click(object sender, RoutedEventArgs e)
        {
            if (accountStore == null)
            {
                FormMessageTextBlock.Text = "Accounts are not loaded.";
                return;
            }

            if (SelectedAccount == null)
            {
                FormMessageTextBlock.Text = "Select an account to edit.";
                return;
            }

            ShowCreateAccountForm(true, SelectedAccount);
        }

        private void CancelCreateAccount_Click(object sender, RoutedEventArgs e)
        {
            ShowCreateAccountForm(false);
        }

        /// <summary>Shows the account form for a new account, or for editing <paramref name="accountToEdit"/>.</summary>
        private void ShowCreateAccountForm(bool show, Account? accountToEdit = null)
        {
            editingAccount = show ? accountToEdit : null;

            AccountListPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            CreateAccountPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            FormMessageTextBlock.Text = "";

            if (show)
            {
                var isEdit = accountToEdit != null;

                AccountFormTitle.Text = isEdit ? "Edit Account" : "New Account";
                AccountFormHint.Text = isEdit
                    ? "Change the account name or category."
                    : "Create an account with its opening balance.";
                SaveAccountButton.Content = isEdit ? "Save" : "+ Create";

                AccountNameTextBox.Text = accountToEdit?.Name ?? "";

                // Read the categories each time, so changes made in Settings show up here.
                var categories = accountStore!.GetAccountCategories();
                CategoryComboBox.ItemsSource = categories;
                CategoryComboBox.SelectedItem = isEdit
                    ? categories.FirstOrDefault(c => c.Id == accountToEdit!.CategoryId)
                    : categories.FirstOrDefault();

                OpeningBalanceTextBox.Text = isEdit ? accountToEdit!.OpeningBalance.ToString("N2") : "0";
                OpeningBalanceTextBox.IsEnabled = !isEdit;
                AccountNameTextBox.Focus();
            }
        }

        private void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            if (accountStore == null)
            {
                FormMessageTextBlock.Text = "Accounts are not loaded.";
                return;
            }

            if (SelectedAccount == null)
            {
                FormMessageTextBlock.Text = "Select an account to delete.";
                return;
            }

            var confirmation = System.Windows.MessageBox.Show(
                this,
                $"Delete account \"{SelectedAccount.Name}\"?",
                "Delete Account",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            var removedIndex = Accounts.IndexOf(SelectedAccount);
            accountStore.DeleteAccount(SelectedAccount.Id);
            Accounts.Remove(SelectedAccount);

            if (Accounts.Count == 0)
            {
                SelectedAccount = null;
            }
            else
            {
                SelectedAccount = Accounts[Math.Min(removedIndex, Accounts.Count - 1)];
            }

            FormMessageTextBlock.Text = "";
        }

        private static bool TryParseAmount(string text, out decimal amount)
        {
            return decimal.TryParse(text, NumberStyles.Currency, CultureInfo.CurrentCulture, out amount) ||
                   decimal.TryParse(text, NumberStyles.Currency, CultureInfo.InvariantCulture, out amount);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
