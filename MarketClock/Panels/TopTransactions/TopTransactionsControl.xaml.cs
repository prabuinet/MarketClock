using System.Windows;
using MarketClock.Data;

namespace MarketClock.Panels
{
    // Top Transactions This Month panel: this month's largest transactions, in or out.
    // Transfers between accounts are left out, as no money comes in or goes out.
    public partial class TopTransactionsControl : System.Windows.Controls.UserControl
    {
        public TopTransactionsControl()
        {
            InitializeComponent();
        }

        public void ShowAccounts(IReadOnlyList<Account> accounts)
        {
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var nextMonthStart = monthStart.AddMonths(1);

            var top = DashboardTransactions.Activity(accounts)
                .Where(x => !x.Transaction.IsTransfer && x.Transaction.Date >= monthStart && x.Transaction.Date < nextMonthStart)
                .OrderByDescending(x => Math.Abs(x.Transaction.Amount))
                .Take(DashboardTransactions.RowCount)
                .Select(x => DashboardTransactions.ToRow(x.Account, x.Transaction))
                .ToList();

            TopTransactionsList.ItemsSource = top;
            TopEmptyText.Text = "No transactions this month.";
            TopEmptyText.Visibility = top.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ShowError(string message)
        {
            TopEmptyText.Text = message;
            TopEmptyText.Visibility = Visibility.Visible;
        }
    }
}
