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

        /// <summary>Account balance after this transaction was applied.</summary>
        public decimal Balance { get; set; }
    }
}
