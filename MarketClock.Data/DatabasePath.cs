namespace MarketClock.Data
{
    public static class DatabasePath
    {
        /// <summary>%LocalAppData%\MarketClock\marketclock.db</summary>
        public static string Default
        {
            get
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MarketClock");
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, "marketclock.db");
            }
        }
    }
}
