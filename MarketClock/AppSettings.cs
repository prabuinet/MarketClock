using System.IO;
using System.Text.Json;

namespace MarketClock
{
    /// <summary>
    /// App-wide settings, kept in %LocalAppData%\MarketClock\settings.json.
    /// Add a property here to add a setting; missing values fall back to the defaults below.
    /// </summary>
    public sealed class AppSettings
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarketClock",
            "settings.json");

        /// <summary>Folder the Songs panel browses.</summary>
        public string SongsFolder { get; set; } = @"D:\songs";

        /// <summary>Which equalizer look is selected (index into EqualizerShaders.Styles).</summary>
        public int EqualizerStyle { get; set; }

        /// <summary>First hour of the day the hourly bell rings at, as 24-hour "HH:mm".</summary>
        public string HourlyBellStart { get; set; } = "07:00";

        /// <summary>Last hour of the day the hourly bell rings at, as 24-hour "HH:mm".</summary>
        public string HourlyBellEnd { get; set; } = "23:00";

        /// <summary>Period the Daily Expenses panel shows: "Week", "Month" or "Year".</summary>
        public string DailyExpensesRange { get; set; } = "Month";

        /// <summary>True when the Daily Expenses panel shows its chart, false for the table.</summary>
        public bool DailyExpensesChart { get; set; }

        /// <summary>True when the This Month panel shows its bar chart, false for the figures.</summary>
        public bool MonthSummaryChart { get; set; }

        /// <summary>
        /// The time buttons of the Countdown panel, each in seconds, in the order shown.
        /// Null until the buttons are first changed, which means the standard 10, 15 and 20 minutes.
        /// </summary>
        public List<int>? CountdownButtons { get; set; }

        /// <summary>Reads a time of day typed as "7:00 AM", "07:00" or "23:00"; null when it is not a time.</summary>
        public static TimeSpan? ParseTimeOfDay(string? text)
        {
            return DateTime.TryParse(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.NoCurrentDateDefault,
                out var parsed)
                ? parsed.TimeOfDay
                : null;
        }

        /// <summary>Shows a time of day the way the dashboard does, e.g. "7:00 AM".</summary>
        public static string FormatTimeOfDay(TimeSpan time) =>
            DateTime.Today.Add(time).ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Unreadable settings: carry on with the defaults.
            }

            return new AppSettings();
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
