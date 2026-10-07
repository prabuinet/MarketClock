using System.Windows;
using MarketClock.Data;

namespace MarketClock.Panels
{
    // Recent Transactions panel: the latest few transactions across all accounts.
    public partial class RecentTransactionsControl : System.Windows.Controls.UserControl
    {
        public RecentTransactionsControl()
        {
            InitializeComponent();
        }

        public void ShowAccounts(IReadOnlyList<Account> accounts)
        {
            var recent = DashboardTransactions.Activity(accounts)
                .OrderByDescending(x => x.Transaction.Date)
                .ThenByDescending(x => x.Transaction.Id)
                .Take(DashboardTransactions.RowCount)
                .Select(x => DashboardTransactions.ToRow(x.Account, x.Transaction))
                .ToList();

            RecentTransactionsList.ItemsSource = recent;
            RecentEmptyText.Text = "No transactions yet.";
            RecentEmptyText.Visibility = recent.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ShowError(string message)
        {
            RecentEmptyText.Text = message;
            RecentEmptyText.Visibility = Visibility.Visible;
        }
    }
}
