using MarketClock.Data;

namespace MarketClock
{
    // Loads the accounts and hands them to the dashboard's money panels: net worth,
    // net worth graph, accounts, recent transactions, this month's largest transactions
    // daily expenses and this month's income and expense.
    public partial class MainWindow
    {
        /// <summary>Reloads the money panels from the database without blocking the window.</summary>
        private async void RefreshDashboard()
        {
            try
            {
                var accounts = await Task.Run(() => new AccountStore().GetAccounts());

                NetWorthView.ShowAccounts(accounts);
                NetWorthGraphView.ShowAccounts(accounts);
                AccountsView.ShowAccounts(accounts);
                RecentView.ShowAccounts(accounts);
                TopView.ShowAccounts(accounts);
                DailyExpensesView.ShowAccounts(accounts);
                MonthSummaryView.ShowAccounts(accounts);
            }
            catch (Exception ex)
            {
                var message = $"Could not load: {ex.Message}";

                NetWorthView.ShowError(message);
                AccountsView.ShowError(message);
                RecentView.ShowError(message);
                TopView.ShowError(message);
                DailyExpensesView.ShowError(message);
                MonthSummaryView.ShowError(message);
            }
        }
    }
}
