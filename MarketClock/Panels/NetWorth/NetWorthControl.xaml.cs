using MarketClock.Data;

namespace MarketClock.Panels
{
    // Net Worth panel: the balances of all the accounts added together.
    public partial class NetWorthControl : System.Windows.Controls.UserControl
    {
        public NetWorthControl()
        {
            InitializeComponent();
        }

        public void ShowAccounts(IReadOnlyList<Account> accounts)
        {
            NetWorthText.Text = accounts.Sum(a => a.Balance).ToString("N2");
            NetWorthHint.Text = accounts.Count == 1 ? "across 1 account" : $"across {accounts.Count} accounts";
        }

        public void ShowError(string message)
        {
            NetWorthText.Text = "-";
            NetWorthHint.Text = message;
        }
    }
}
