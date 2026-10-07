using System.Windows;
using MarketClock.Data;
using Media = System.Windows.Media;

namespace MarketClock.Panels
{
    // This Month panel: the current month's expense and income, either as two figures
    // side by side or as two horizontal bars drawn to the same scale.
    // Expense is money going out of an account and income is money coming in;
    // opening balances, daily MTM entries and transfers between accounts count as neither.
    public partial class MonthSummaryControl : System.Windows.Controls.UserControl
    {
        private bool chartMode;

        public MonthSummaryControl()
        {
            InitializeComponent();
            UpdateMode();
        }

        /// <summary>Restores the mode that was last chosen.</summary>
        public void LoadSettings()
        {
            chartMode = AppSettings.Load().MonthSummaryChart;
            UpdateMode();
        }

        public void ShowAccounts(IReadOnlyList<Account> accounts)
        {
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var nextMonthStart = monthStart.AddMonths(1);

            var amounts = DashboardTransactions.Activity(accounts)
                .Select(x => x.Transaction)
                .Where(t => !t.IsDailyMtm && !t.IsTransfer && t.Date >= monthStart && t.Date < nextMonthStart)
                .Select(t => t.Amount)
                .ToList();

            var income = amounts.Where(amount => amount > 0).Sum();
            var expense = -amounts.Where(amount => amount < 0).Sum();
            var net = income - expense;

            SummaryText.Text = $"{monthStart:MMMM yyyy}  ·  net {(net > 0 ? "+" : "")}{net:N2}";

            ExpenseText.Text = expense.ToString("N2");
            IncomeText.Text = income.ToString("N2");
            ExpenseBarText.Text = expense.ToString("N2");
            IncomeBarText.Text = income.ToString("N2");

            // Both bars share one scale: the larger of the two fills the width.
            var max = Math.Max(income, expense);
            SetBar(ExpenseBarColumn, ExpenseRestColumn, max > 0 ? (double)(expense / max) : 0);
            SetBar(IncomeBarColumn, IncomeRestColumn, max > 0 ? (double)(income / max) : 0);
        }

        public void ShowError(string message)
        {
            SummaryText.Text = message;
        }

        private static void SetBar(
            System.Windows.Controls.ColumnDefinition bar, System.Windows.Controls.ColumnDefinition rest, double fraction)
        {
            bar.Width = new GridLength(fraction, GridUnitType.Star);
            rest.Width = new GridLength(1 - fraction, GridUnitType.Star);
        }

        private void TextMode_Click(object sender, RoutedEventArgs e) => SetChartMode(false);

        private void ChartMode_Click(object sender, RoutedEventArgs e) => SetChartMode(true);

        private void SetChartMode(bool chart)
        {
            if (chartMode == chart)
            {
                return;
            }

            chartMode = chart;
            UpdateMode();

            try
            {
                var settings = AppSettings.Load();
                settings.MonthSummaryChart = chartMode;
                settings.Save();
            }
            catch (Exception)
            {
                // Not being able to remember the choice should not interrupt anything.
            }
        }

        private void UpdateMode()
        {
            TextView.Visibility = chartMode ? Visibility.Collapsed : Visibility.Visible;
            ChartView.Visibility = chartMode ? Visibility.Visible : Visibility.Collapsed;
            Theme.SetAccent(TextModeButton, ForegroundProperty, !chartMode, Media.Brushes.White);
            Theme.SetAccent(ChartModeButton, ForegroundProperty, chartMode, Media.Brushes.White);
        }
    }
}
