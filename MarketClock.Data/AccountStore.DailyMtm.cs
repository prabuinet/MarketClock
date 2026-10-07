using Microsoft.EntityFrameworkCore;

namespace MarketClock.Data
{
    /// <summary>One trading account on the Daily MTM form: its balance now and the MTM already entered for the day, if any.</summary>
    public sealed record DailyMtmEntry(int AccountId, string AccountName, decimal Balance, decimal? Mtm);

    // Daily MTM: the day's profit or loss of each trading account, kept as one marked
    // transaction per account per day so it can be entered once and corrected later.
    public partial class AccountStore
    {
        public const string DailyMtmDescription = "Daily MTM";

        /// <summary>A trading account is one whose category name mentions "trading" (e.g. "Trading account").</summary>
        private static bool IsTradingAccount(Account account) =>
            account.Category?.Name.Contains("trading", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>Every trading account, with the MTM recorded for <paramref name="date"/> (null when none has been entered).</summary>
        public List<DailyMtmEntry> GetDailyMtm(DateTime date)
        {
            using var db = CreateContext();

            var day = date.Date;
            var nextDay = day.AddDays(1);

            var accounts = db.Accounts
                .AsNoTracking()
                .Include(a => a.Category)
                .OrderBy(a => a.Id)
                .ToList()
                .Where(IsTradingAccount)
                .ToList();

            var entered = db.Transactions
                .AsNoTracking()
                .Where(t => t.IsDailyMtm && t.Date >= day && t.Date < nextDay)
                .ToList();

            return accounts
                .Select(account =>
                {
                    var forAccount = entered.Where(t => t.AccountId == account.Id).ToList();
                    return new DailyMtmEntry(
                        account.Id,
                        account.Name,
                        account.Balance,
                        forAccount.Count == 0 ? null : forAccount.Sum(t => t.Amount));
                })
                .ToList();
        }

        /// <summary>
        /// Records the day's MTM for each account in <paramref name="amounts"/>: a number adds the entry
        /// or changes the one already there, null removes it. Each account that changed is then
        /// reconciled, so its balance and the running balance on every transaction match again.
        /// All of it is saved together, or none of it.
        /// </summary>
        public void SaveDailyMtm(DateTime date, IReadOnlyDictionary<int, decimal?> amounts)
        {
            using var db = CreateContext();
            using var dbTransaction = db.Database.BeginTransaction();

            var day = date.Date;
            var nextDay = day.AddDays(1);

            foreach (var (accountId, amount) in amounts)
            {
                var account = db.Accounts.Find(accountId)
                    ?? throw new InvalidOperationException($"Account {accountId} does not exist.");

                var existing = db.Transactions
                    .Where(t => t.AccountId == accountId && t.IsDailyMtm && t.Date >= day && t.Date < nextDay)
                    .OrderBy(t => t.Id)
                    .ToList();

                if (amount == null)
                {
                    if (existing.Count == 0)
                    {
                        continue;
                    }

                    db.Transactions.RemoveRange(existing);
                }
                else if (existing.Count == 0)
                {
                    db.Transactions.Add(new AccountTransaction
                    {
                        AccountId = accountId,
                        Date = day,
                        Description = DailyMtmDescription,
                        Amount = amount.Value,
                        IsDailyMtm = true,
                    });
                }
                else if (existing.Count == 1 && existing[0].Amount == amount.Value)
                {
                    continue; // nothing changed for this account
                }
                else
                {
                    existing[0].Amount = amount.Value;
                    db.Transactions.RemoveRange(existing.Skip(1));
                }

                db.SaveChanges();
                Reconcile(db, account);
                db.SaveChanges();
            }

            dbTransaction.Commit();
        }

        /// <summary>
        /// Recalculates the running balance on every transaction of the account, in date order,
        /// and sets the account balance to the final figure.
        /// </summary>
        private static void Reconcile(MarketClockDbContext db, Account account)
        {
            var transactions = db.Transactions
                .Where(t => t.AccountId == account.Id)
                .ToList()
                .OrderBy(t => t.Date)
                .ThenBy(t => t.Id);

            var running = 0m;
            foreach (var transaction in transactions)
            {
                running += transaction.Amount;
                transaction.Balance = running;
            }

            account.Balance = running;
        }
    }
}
