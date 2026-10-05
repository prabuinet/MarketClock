namespace MarketClock.Data
{
    /// <summary>A kind of expense, e.g. "Food" or "Rent".</summary>
    public class ExpenseCategory : ICategory
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";
    }
}
