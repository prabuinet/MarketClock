using MarketClock.Data;

namespace MarketClock.Panels
{
    /// <summary>One line in a dashboard transaction list.</summary>
    public sealed record DashboardTransactionRow(
        string Date,
        string Title,
        string Account,
        decimal Amount,
        int TransactionId,
        bool IsTransfer)
    {
        public bool IsDebit => Amount < 0;

        /// <summary>The whole line as text, for asking "delete this one?".</summary>
        public string Summary => $"{Date} · {Title} · {Account} · {Amount:N2}";
    }

    // What the Recent Transactions and Top Transactions panels have in common.
    internal static class DashboardTransactions
    {
        /// <summary>How many transactions each of the two panels lists.</summary>
        public const int RowCount = 5;

        /// <summary>Every transaction that is real activity, with the account it belongs to.</summary>
        public static List<(Account Account, AccountTransaction Transaction)> Activity(IEnumerable<Account> accounts)
        {
            // The first transaction of an account is its opening balance, not real activity.
            return accounts
                .SelectMany(account => account.Transactions
                    .OrderBy(t => t.Id)
                    .Skip(1)
                    .Select(t => (Account: account, Transaction: t)))
                .ToList();
        }

        public static DashboardTransactionRow ToRow(Account account, AccountTransaction transaction)
        {
            var title = !string.IsNullOrWhiteSpace(transaction.Description)
                ? transaction.Description
                : transaction.ExpenseCategory?.Name ?? "(no description)";

            return new DashboardTransactionRow(
                transaction.Date.ToString("dd MMM"),
                title,
                account.Name,
                transaction.Amount,
                transaction.Id,
                transaction.IsTransfer);
        }
    }
}
