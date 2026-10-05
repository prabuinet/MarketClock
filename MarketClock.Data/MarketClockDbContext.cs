using Microsoft.EntityFrameworkCore;

namespace MarketClock.Data
{
    public class MarketClockDbContext : DbContext
    {
        private readonly string? databasePath;

        /// <summary>Uses the default database file (see <see cref="DatabasePath.Default"/>).</summary>
        public MarketClockDbContext()
        {
        }

        public MarketClockDbContext(string databasePath)
        {
            this.databasePath = databasePath;
        }

        public MarketClockDbContext(DbContextOptions<MarketClockDbContext> options)
            : base(options)
        {
        }

        public DbSet<Account> Accounts => Set<Account>();

        public DbSet<AccountTransaction> Transactions => Set<AccountTransaction>();

        public DbSet<AccountCategory> AccountCategories => Set<AccountCategory>();

        public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlite($"Data Source={databasePath ?? DatabasePath.Default}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>(account =>
            {
                account.ToTable("Accounts");
                account.Property(a => a.Name).IsRequired().HasMaxLength(200);

                account.HasOne(a => a.Category)
                    .WithMany()
                    .HasForeignKey(a => a.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Soft-deleted accounts stay in the file but are left out of every query.
                account.HasQueryFilter(a => a.DeletedAt == null);

                account.HasMany(a => a.Transactions)
                    .WithOne(t => t.Account)
                    .HasForeignKey(t => t.AccountId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<AccountCategory>(category =>
            {
                category.ToTable("AccountCategories");
                category.Property(c => c.Name).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<ExpenseCategory>(category =>
            {
                category.ToTable("ExpenseCategories");
                category.Property(c => c.Name).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<AccountTransaction>(transaction =>
            {
                transaction.ToTable("Transactions");
                transaction.Property(t => t.Description).IsRequired().HasMaxLength(500);
                transaction.HasIndex(t => new { t.AccountId, t.Date });

                transaction.HasOne(t => t.ExpenseCategory)
                    .WithMany()
                    .HasForeignKey(t => t.ExpenseCategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
