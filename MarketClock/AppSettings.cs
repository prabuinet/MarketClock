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
