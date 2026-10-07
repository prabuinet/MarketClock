using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MarketClock.Data;

namespace MarketClock.Panels
{
    // Recent Transactions panel: the latest few transactions across all accounts.
    // Double-click one to edit it; right-click it to edit or delete it.
    public partial class RecentTransactionsControl : System.Windows.Controls.UserControl
    {
        // The transaction the right-click menu was opened on.
        private DashboardTransactionRow? menuRow;

        public RecentTransactionsControl()
        {
            InitializeComponent();
        }

        /// <summary>Raised after a transaction was edited or deleted here, so the dashboard can reload.</summary>
        public event EventHandler? TransactionsChanged;

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

        private void RecentTransactionsList_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2 || RowUnderMouse() is not { } row)
            {
                return;
            }

            e.Handled = true;

            // Open the form once this click has finished travelling up to the window.
            Dispatcher.BeginInvoke(new Action(() => Edit(row)));
        }

        private void RecentTransactionsList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            menuRow = RowUnderMouse();

            if (menuRow == null)
            {
                e.Handled = true; // not on a transaction: no menu
            }
        }

        private void EditMenu_Click(object sender, RoutedEventArgs e)
        {
            if (menuRow != null)
            {
                Edit(menuRow);
            }
        }

        private void DeleteMenu_Click(object sender, RoutedEventArgs e)
        {
            if (menuRow != null
                && TransactionActions.Delete(Window.GetWindow(this), menuRow.TransactionId, menuRow.Summary, menuRow.IsTransfer))
            {
                TransactionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Edit(DashboardTransactionRow row)
        {
            if (TransactionActions.Edit(Window.GetWindow(this), row.TransactionId))
            {
                TransactionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>The transaction whose line the mouse is on, or null.</summary>
        private DashboardTransactionRow? RowUnderMouse()
        {
            foreach (var item in RecentTransactionsList.Items)
            {
                if (RecentTransactionsList.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement line)
                {
                    var y = Mouse.GetPosition(line).Y;
                    if (y >= 0 && y < line.ActualHeight)
                    {
                        return item as DashboardTransactionRow;
                    }
                }
            }

            return null;
        }
    }
}
