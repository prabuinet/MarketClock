using System.Windows;
using MarketClock.Data;

namespace MarketClock.Panels
{
    // Accounts panel: each account with its balance.
    public partial class AccountsControl : System.Windows.Controls.UserControl
    {
        public AccountsControl()
        {
            InitializeComponent();
        }

        public void ShowAccounts(IReadOnlyList<Account> accounts)
        {
            DashboardAccountsList.ItemsSource = accounts;
            AccountsEmptyText.Text = "No accounts yet.";
            AccountsEmptyText.Visibility = accounts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ShowError(string message)
        {
            AccountsEmptyText.Text = message;
            AccountsEmptyText.Visibility = Visibility.Visible;
        }
    }
}
