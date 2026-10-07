namespace MarketClock.Data
{
    public class AccountTransaction
    {
        public int Id { get; set; }

        public int AccountId { get; set; }

        public Account? Account { get; set; }

        public DateTime Date { get; set; }

        public string Description { get; set; } = "";

        public decimal Amount { get; set; }

        /// <summary>Set for expenses that were given a category; null otherwise.</summary>
        public int? ExpenseCategoryId { get; set; }

        public ExpenseCategory? ExpenseCategory { get; set; }

        /// <summary>True for the day's profit or loss entered through the Daily MTM form (one per account per day).</summary>
        public bool IsDailyMtm { get; set; }

        /// <summary>True for either side of a transfer between two accounts: not an expense and not an income.</summary>
        public bool IsTransfer { get; set; }

        /// <summary>Account balance after this transaction was applied.</summary>
        public decimal Balance { get; set; }
    }
}
