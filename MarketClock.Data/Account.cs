namespace MarketClock.Data
{
    public class Account
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public int? CategoryId { get; set; }

        public AccountCategory? Category { get; set; }

        /// <summary>Category name for display, e.g. "Bank account". Not stored.</summary>
        public string CategoryName => Category?.Name ?? "";

        public decimal OpeningBalance { get; set; }

        /// <summary>Current balance: opening balance plus every transaction since.</summary>
        public decimal Balance { get; set; }

        /// <summary>When the account was soft-deleted; null while the account is active.</summary>
        public DateTime? DeletedAt { get; set; }

        public List<AccountTransaction> Transactions { get; set; } = new();
    }
}
