using System.Windows;
using MarketClock.Data;

namespace MarketClock
{
    // Data shown on the dashboard's money panels: net worth, accounts,
    // recent transactions and this month's largest transactions.
    public partial class MainWindow
    {
        private const int DashboardRowCount = 5;

        /// <summary>One line in a dashboard transaction list.</summary>
        public sealed record DashboardTransactionRow(string Date, string Title, string Account, decimal Amount)
        {
            public bool IsDebit => Amount < 0;
        }

        /// <summary>Reloads the money panels from the database without blocking the window.</summary>
        private async void RefreshDashboard()
        {
            try
            {
                var accounts = await Task.Run(() => new AccountStore().GetAccounts());

                // The first transaction of an account is its opening balance, not real activity.
                var transactions = accounts
                    .SelectMany(account => account.Transactions
                        .OrderBy(t => t.Id)
                        .Skip(1)
                        .Select(t => (Account: account, Transaction: t)))
                    .ToList();

                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var nextMonthStart = monthStart.AddMonths(1);

                var recent = transactions
                    .OrderByDescending(x => x.Transaction.Date)
                    .ThenByDescending(x => x.Transaction.Id)
                    .Take(DashboardRowCount)
                    .Select(x => ToRow(x.Account, x.Transaction))
                    .ToList();

                var top = transactions
                    .Where(x => x.Transaction.Date >= monthStart && x.Transaction.Date < nextMonthStart)
                    .OrderByDescending(x => Math.Abs(x.Transaction.Amount))
                    .Take(DashboardRowCount)
                    .Select(x => ToRow(x.Account, x.Transaction))
                    .ToList();

                NetWorthText.Text = accounts.Sum(a => a.Balance).ToString("N2");
                NetWorthHint.Text = accounts.Count == 1 ? "across 1 account" : $"across {accounts.Count} accounts";

                dashboardAccounts = accounts;
                UpdateNetWorthGraph();

                DashboardAccountsList.ItemsSource = accounts;
                AccountsEmptyText.Text = "No accounts yet.";
                AccountsEmptyText.Visibility = ToVisibility(accounts.Count == 0);

                RecentTransactionsList.ItemsSource = recent;
                RecentEmptyText.Text = "No transactions yet.";
                RecentEmptyText.Visibility = ToVisibility(recent.Count == 0);

                TopTransactionsList.ItemsSource = top;
                TopEmptyText.Text = "No transactions this month.";
                TopEmptyText.Visibility = ToVisibility(top.Count == 0);
            }
            catch (Exception ex)
            {
                var message = $"Could not load: {ex.Message}";

                NetWorthText.Text = "-";
                NetWorthHint.Text = message;

                foreach (var emptyText in new[] { AccountsEmptyText, RecentEmptyText, TopEmptyText })
                {
                    emptyText.Text = message;
                    emptyText.Visibility = Visibility.Visible;
                }
            }
        }

        private static DashboardTransactionRow ToRow(Account account, AccountTransaction transaction)
        {
            var title = !string.IsNullOrWhiteSpace(transaction.Description)
                ? transaction.Description
                : transaction.ExpenseCategory?.Name ?? "(no description)";

            return new DashboardTransactionRow(
                transaction.Date.ToString("dd MMM"),
                title,
                account.Name,
                transaction.Amount);
        }
    }
}
