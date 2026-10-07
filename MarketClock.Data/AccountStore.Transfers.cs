namespace MarketClock.Data
{
    // Transfers: an amount moved from one account to another, kept as a pair of marked
    // transactions (money out of one account, the same money into the other).
    public partial class AccountStore
    {
        /// <summary>
        /// Moves <paramref name="amount"/> from one account to another on <paramref name="date"/>.
        /// Both accounts are then reconciled, so a back-dated transfer leaves every running
        /// balance correct. Both sides are saved together, or neither.
        /// </summary>
        public void Transfer(int fromAccountId, int toAccountId, DateTime date, decimal amount, string note = "")
        {
            if (fromAccountId == toAccountId)
            {
                throw new InvalidOperationException("A transfer needs two different accounts.");
            }

            if (amount <= 0)
            {
                throw new InvalidOperationException("The amount to transfer must be greater than zero.");
            }

            using var db = CreateContext();
            using var dbTransaction = db.Database.BeginTransaction();

            var from = db.Accounts.Find(fromAccountId)
                ?? throw new InvalidOperationException($"Account {fromAccountId} does not exist.");
            var to = db.Accounts.Find(toAccountId)
                ?? throw new InvalidOperationException($"Account {toAccountId} does not exist.");

            var suffix = string.IsNullOrWhiteSpace(note) ? "" : $" - {note.Trim()}";

            db.Transactions.Add(new AccountTransaction
            {
                AccountId = from.Id,
                Date = date,
                Description = $"Transfer to {to.Name}{suffix}",
                Amount = -amount,
                IsTransfer = true,
            });

            db.Transactions.Add(new AccountTransaction
            {
                AccountId = to.Id,
                Date = date,
                Description = $"Transfer from {from.Name}{suffix}",
                Amount = amount,
                IsTransfer = true,
            });

            db.SaveChanges();
            Reconcile(db, from);
            Reconcile(db, to);
            db.SaveChanges();

            dbTransaction.Commit();
        }
    }
}
