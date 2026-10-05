namespace MarketClock.Data
{
    /// <summary>Common shape of the category lists managed in Settings.</summary>
    public interface ICategory
    {
        int Id { get; }

        string Name { get; }
    }

    /// <summary>A kind of account, e.g. "Bank account" or "Trading account".</summary>
    public class AccountCategory : ICategory
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";
    }
}
