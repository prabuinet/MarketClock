using Microsoft.EntityFrameworkCore;

namespace MarketClock.Data
{
    /// <summary>
    /// Reads and writes accounts and their transactions in the SQLite file.
    /// Each call opens its own short-lived context, so returned entities are detached.
    /// </summary>
    public partial class AccountStore
    {
        private readonly string databasePath;

        public AccountStore(string? databasePath = null)
        {
            this.databasePath = databasePath ?? DatabasePath.Default;

            using var db = CreateContext();
            db.Database.EnsureCreated();
            DatabaseUpgrader.Upgrade(db);
        }

        /// <summary>All accounts with their transactions, oldest transaction first.</summary>
        public List<Account> GetAccounts()
        {
            using var db = CreateContext();

            var accounts = db.Accounts
                .AsNoTracking()
                .Include(a => a.Category)
                .Include(a => a.Transactions)
                    .ThenInclude(t => t.ExpenseCategory)
                .OrderBy(a => a.Id)
                .ToList();

            foreach (var account in accounts)
            {
                account.Transactions = account.Transactions
                    .OrderBy(t => t.Date)
                    .ThenBy(t => t.Id)
                    .ToList();
            }

            return accounts;
        }

        /// <summary>Creates an account and records its opening balance as the first transaction.</summary>
        public Account CreateAccount(string name, int? categoryId, decimal openingBalance)
        {
            using var db = CreateContext();

            var account = new Account
            {
                Name = name,
                CategoryId = categoryId,
                Category = categoryId == null ? null : db.AccountCategories.Find(categoryId),
                OpeningBalance = openingBalance,
                Balance = openingBalance,
            };

            account.Transactions.Add(new AccountTransaction
            {
                Date = DateTime.Today,
                Description = "Opening balance",
                Amount = openingBalance,
                Balance = openingBalance,
            });

            db.Accounts.Add(account);
            db.SaveChanges();
            return account;
        }

        /// <summary>Changes the name and category of an existing account.</summary>
        public void UpdateAccount(int accountId, string name, int? categoryId)
        {
            using var db = CreateContext();

            var account = db.Accounts.Find(accountId)
                ?? throw new InvalidOperationException($"Account {accountId} does not exist.");

            account.Name = name;
            account.CategoryId = categoryId;
            db.SaveChanges();
        }

        /// <summary>
        /// Appends a transaction and updates the account balance in one save.
        /// Use a negative amount for a debit.
        /// </summary>
        public AccountTransaction AddTransaction(
            int accountId,
            DateTime date,
            string description,
            decimal amount,
            int? expenseCategoryId = null)
        {
            using var db = CreateContext();

            var account = db.Accounts.Find(accountId)
                ?? throw new InvalidOperationException($"Account {accountId} does not exist.");

            account.Balance += amount;

            var transaction = new AccountTransaction
            {
                AccountId = accountId,
                Date = date,
                Description = description,
                Amount = amount,
                ExpenseCategoryId = expenseCategoryId,
                Balance = account.Balance,
            };

            db.Transactions.Add(transaction);
            db.SaveChanges();
            return transaction;
        }

        /// <summary>
        /// Soft-deletes the account: it is marked as deleted and no longer returned,
        /// but the account and its transactions remain in the database file.
        /// </summary>
        public void DeleteAccount(int accountId)
        {
            using var db = CreateContext();

            var account = db.Accounts.Find(accountId);
            if (account == null)
            {
                return;
            }

            account.DeletedAt = DateTime.Now;
            db.SaveChanges();
        }

        private MarketClockDbContext CreateContext() => new(databasePath);
    }
}
