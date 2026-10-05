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
    }
}
