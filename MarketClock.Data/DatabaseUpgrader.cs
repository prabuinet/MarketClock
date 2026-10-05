using Microsoft.EntityFrameworkCore;

namespace MarketClock.Data
{
    /// <summary>
    /// Brings a database file created by an earlier version up to the current schema.
    /// Every step checks before it changes anything, so running it again is harmless.
    /// </summary>
    internal static class DatabaseUpgrader
    {
        private static readonly string[] DefaultAccountCategories =
        {
            "Bank account",
            "Trading account",
            "Loan account",
            "Credit card",
            "Cash",
            "Other",
        };

        public static void Upgrade(MarketClockDbContext db)
        {
            // Soft delete.
            if (!HasColumn(db, "Accounts", "DeletedAt"))
            {
                db.Database.ExecuteSqlRaw("ALTER TABLE Accounts ADD COLUMN DeletedAt TEXT NULL");
            }

            // Category tables.
            db.Database.ExecuteSqlRaw(
                "CREATE TABLE IF NOT EXISTS AccountCategories (" +
                "Id INTEGER NOT NULL CONSTRAINT PK_AccountCategories PRIMARY KEY AUTOINCREMENT, " +
                "Name TEXT NOT NULL)");

            db.Database.ExecuteSqlRaw(
                "CREATE TABLE IF NOT EXISTS ExpenseCategories (" +
                "Id INTEGER NOT NULL CONSTRAINT PK_ExpenseCategories PRIMARY KEY AUTOINCREMENT, " +
                "Name TEXT NOT NULL)");

            if (!db.AccountCategories.Any())
            {
                db.AccountCategories.AddRange(DefaultAccountCategories.Select(name => new AccountCategory { Name = name }));
                db.SaveChanges();
            }

            // Accounts point at a category row instead of holding a fixed category name.
            if (!HasColumn(db, "Accounts", "CategoryId"))
            {
                db.Database.ExecuteSqlRaw(
                    "ALTER TABLE Accounts ADD COLUMN CategoryId INTEGER NULL REFERENCES AccountCategories (Id) ON DELETE SET NULL");
            }

            if (HasColumn(db, "Accounts", "Category"))
            {
                db.Database.ExecuteSqlRaw(
                    "UPDATE Accounts SET CategoryId = (" +
                    "SELECT Id FROM AccountCategories WHERE Name = CASE Accounts.Category " +
                    "WHEN 'Bank' THEN 'Bank account' " +
                    "WHEN 'Trading' THEN 'Trading account' " +
                    "WHEN 'Loan' THEN 'Loan account' " +
                    "WHEN 'CreditCard' THEN 'Credit card' " +
                    "WHEN 'Cash' THEN 'Cash' " +
                    "ELSE 'Other' END) " +
                    "WHERE CategoryId IS NULL");

                db.Database.ExecuteSqlRaw("ALTER TABLE Accounts DROP COLUMN Category");
            }

            // Expenses can carry an expense category.
            AddExpenseCategoryColumn(db);
        }

        private static void AddExpenseCategoryColumn(MarketClockDbContext db)
        {
            if (!HasColumn(db, "Transactions", "ExpenseCategoryId"))
            {
                db.Database.ExecuteSqlRaw(
                    "ALTER TABLE Transactions ADD COLUMN ExpenseCategoryId INTEGER NULL REFERENCES ExpenseCategories (Id) ON DELETE SET NULL");
            }
        }

        private static bool HasColumn(MarketClockDbContext db, string table, string column)
        {
            return db.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM pragma_table_info({table}) WHERE name = {column}")
                .AsEnumerable()
                .Single() > 0;
        }
    }
}
