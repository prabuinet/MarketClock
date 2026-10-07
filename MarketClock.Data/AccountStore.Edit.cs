using Microsoft.EntityFrameworkCore;

namespace MarketClock.Data
{
    // Changing and deleting transactions that are already saved. Every account that is touched
    // is reconciled afterwards, so its balance and all running balances stay correct.
    public partial class AccountStore
    {
        /// <summary>
        /// Changes a saved transaction. <paramref name="amount"/> is signed: negative for money out.
        /// A transfer keeps both of its sides in step (same date, opposite amounts). Only an ordinary
        /// expense or income can be moved to another account. All of it is saved together, or none of it.
        /// </summary>
        public void UpdateTransaction(
            int transactionId,
            int accountId,
            DateTime date,
            string description,
            decimal amount,
            int? expenseCategoryId)
        {
            using var db = CreateContext();
            using var dbTransaction = db.Database.BeginTransaction();

            var transaction = db.Transactions.Find(transactionId)
                ?? throw new InvalidOperationException("That transaction no longer exists.");

            var oldAccountId = transaction.AccountId;
            var isOpeningBalance = IsOpeningBalance(db, transaction);
            var partner = FindTransferPartner(db, transaction);
            var touchedAccountIds = new HashSet<int> { oldAccountId, accountId };

            if (accountId != oldAccountId)
            {
                if (isOpeningBalance || transaction.IsTransfer || transaction.IsDailyMtm)
                {
                    throw new InvalidOperationException("This transaction cannot be moved to another account.");
                }

                if (db.Accounts.Find(accountId) == null)
                {
                    throw new InvalidOperationException($"Account {accountId} does not exist.");
                }
            }

            if (transaction.IsDailyMtm && date.Date != transaction.Date.Date)
            {
                var day = date.Date;
                var nextDay = day.AddDays(1);

                var taken = db.Transactions.Any(t =>
                    t.AccountId == oldAccountId && t.IsDailyMtm && t.Id != transactionId && t.Date >= day && t.Date < nextDay);

                if (taken)
                {
                    throw new InvalidOperationException("This account already has a Daily MTM for that day.");
                }
            }

            if (accountId != oldAccountId)
            {
                // Saved again as a new row: an account's oldest row is its opening balance,
                // and a moved row must never end up older than the one already there.
                db.Transactions.Remove(transaction);
                db.Transactions.Add(new AccountTransaction
                {
                    AccountId = accountId,
                    Date = date,
                    Description = description,
                    Amount = amount,
                    ExpenseCategoryId = expenseCategoryId,
                });
            }
            else
            {
                transaction.Date = date;
                transaction.Description = description;
                transaction.Amount = amount;
                transaction.ExpenseCategoryId = expenseCategoryId;
            }

            if (partner != null)
            {
                partner.Date = date;
                partner.Amount = -amount;
                touchedAccountIds.Add(partner.AccountId);
            }

            db.SaveChanges();

            foreach (var id in touchedAccountIds)
            {
                var account = FindAccountEvenIfDeleted(db, id);
                Reconcile(db, account);

                if (isOpeningBalance && id == oldAccountId)
                {
                    account.OpeningBalance = amount;
                }
            }

            db.SaveChanges();
            dbTransaction.Commit();
        }

        /// <summary>
        /// Deletes a saved transaction; for a transfer, both of its sides. An account's opening
        /// balance cannot be deleted (edit its amount instead).
        /// </summary>
        public void DeleteTransaction(int transactionId)
        {
            using var db = CreateContext();
            using var dbTransaction = db.Database.BeginTransaction();

            var transaction = db.Transactions.Find(transactionId);
            if (transaction == null)
            {
                return;
            }

            if (IsOpeningBalance(db, transaction))
            {
                throw new InvalidOperationException("An opening balance cannot be deleted. Edit it to change the amount.");
            }

            var touchedAccountIds = new HashSet<int> { transaction.AccountId };

            var partner = FindTransferPartner(db, transaction);
            if (partner != null)
            {
                touchedAccountIds.Add(partner.AccountId);
                db.Transactions.Remove(partner);
            }

            db.Transactions.Remove(transaction);
            db.SaveChanges();

            foreach (var id in touchedAccountIds)
            {
                Reconcile(db, FindAccountEvenIfDeleted(db, id));
            }

            db.SaveChanges();
            dbTransaction.Commit();
        }

        /// <summary>The oldest row of an account is its opening balance.</summary>
        private static bool IsOpeningBalance(MarketClockDbContext db, AccountTransaction transaction) =>
            !db.Transactions.Any(t => t.AccountId == transaction.AccountId && t.Id < transaction.Id);

        /// <summary>
        /// The other side of a transfer, or null. The two sides are not linked in the file, but they
        /// are saved together, so the other side is the neighbouring row with the same date and the
        /// opposite amount in a different account.
        /// </summary>
        private static AccountTransaction? FindTransferPartner(MarketClockDbContext db, AccountTransaction transaction)
        {
            if (!transaction.IsTransfer)
            {
                return null;
            }

            foreach (var id in new[] { transaction.Id + 1, transaction.Id - 1 })
            {
                var other = db.Transactions.Find(id);

                if (other != null
                    && other.IsTransfer
                    && other.AccountId != transaction.AccountId
                    && other.Amount == -transaction.Amount
                    && other.Date == transaction.Date)
                {
                    return other;
                }
            }

            return null;
        }

        /// <summary>The other side of a transfer may sit in an account that has since been deleted.</summary>
        private static Account FindAccountEvenIfDeleted(MarketClockDbContext db, int accountId) =>
            db.Accounts.IgnoreQueryFilters().First(a => a.Id == accountId);
    }
}
