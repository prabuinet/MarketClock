namespace MarketClock
{
    public partial class BalanceControl : System.Windows.Controls.UserControl
    {
        public BalanceControl()
        {
            InitializeComponent();
        }

        /// <summary>Raised when the add expense button is clicked.</summary>
        public event System.Windows.RoutedEventHandler? ExpenseRequested;

        private void AddExpenseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ExpenseRequested?.Invoke(this, e);
        }

        /// <summary>Raised when the daily mtm button is clicked.</summary>
        public event System.Windows.RoutedEventHandler? DailyMtmRequested;

        private void DailyMtmButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DailyMtmRequested?.Invoke(this, e);
        }

        /// <summary>Raised when the income button is clicked.</summary>
        public event System.Windows.RoutedEventHandler? IncomeRequested;

        private void AddIncomeButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            IncomeRequested?.Invoke(this, e);
        }

        /// <summary>Raised when the transfer button is clicked.</summary>
        public event System.Windows.RoutedEventHandler? TransferRequested;

        private void TransferButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            TransferRequested?.Invoke(this, e);
        }
    }
}
