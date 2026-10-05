namespace MarketClock.Data
{
    public partial class AccountStore
    {
        public List<AccountCategory> GetAccountCategories()
        {
            using var db = CreateContext();
            return db.AccountCategories.OrderBy(c => c.Id).ToList();
        }

        public AccountCategory AddAccountCategory(string name)
        {
            using var db = CreateContext();

            var category = new AccountCategory { Name = name };
            db.AccountCategories.Add(category);
            db.SaveChanges();
            return category;
        }

        public void RenameAccountCategory(int categoryId, string name)
        {
            using var db = CreateContext();

            var category = db.AccountCategories.Find(categoryId)
                ?? throw new InvalidOperationException($"Account category {categoryId} does not exist.");

            category.Name = name;
            db.SaveChanges();
        }

        public List<ExpenseCategory> GetExpenseCategories()
        {
            using var db = CreateContext();
            return db.ExpenseCategories.OrderBy(c => c.Id).ToList();
        }

        public ExpenseCategory AddExpenseCategory(string name)
        {
            using var db = CreateContext();

            var category = new ExpenseCategory { Name = name };
            db.ExpenseCategories.Add(category);
            db.SaveChanges();
            return category;
        }

        public void RenameExpenseCategory(int categoryId, string name)
        {
            using var db = CreateContext();

            var category = db.ExpenseCategories.Find(categoryId)
                ?? throw new InvalidOperationException($"Expense category {categoryId} does not exist.");

            category.Name = name;
            db.SaveChanges();
        }
    }
}
