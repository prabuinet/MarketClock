using System.IO;
using System.Text.Json;

namespace MarketClock
{
    /// <summary>One panel's place in a column: which panel, and its share of the column's height.</summary>
    public sealed class DashboardSlot
    {
        public string Key { get; set; } = "";

        public double Weight { get; set; } = 1;
    }

    /// <summary>A column (or the bottom strip): its share of the space and the panels stacked in it, top to bottom.</summary>
    public sealed class DashboardZone
    {
        public double Weight { get; set; } = 1;

        public List<DashboardSlot> Panels { get; set; } = new();
    }

    /// <summary>
    /// Where every dashboard panel sits and how big it is. Saved to
    /// %LocalAppData%\MarketClock\layout.json whenever the user rearranges or resizes.
    /// Sizes are shares ("weights"), not pixels, so the layout scales with the window.
    /// </summary>
    public sealed class DashboardLayout
    {
        public const int ColumnCount = 4;

        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarketClock",
            "layout.json");

        public List<DashboardZone> Columns { get; set; } = new();

        /// <summary>The strip that runs under all the columns.</summary>
        public DashboardZone Bottom { get; set; } = new();

        /// <summary>Share of the height taken by the columns (the bottom strip's share is Bottom.Weight).</summary>
        public double MainWeight { get; set; } = 5;

        [System.Text.Json.Serialization.JsonIgnore]
        public IEnumerable<DashboardZone> AllZones => Columns.Append(Bottom);

        public static DashboardLayout CreateDefault()
        {
            static DashboardZone Zone(double weight, params (string Key, double Weight)[] panels) => new()
            {
                Weight = weight,
                Panels = panels.Select(p => new DashboardSlot { Key = p.Key, Weight = p.Weight }).ToList(),
            };

            return new DashboardLayout
            {
                MainWeight = 5,
                Columns =
                {
                    Zone(1, ("Clock", 5), ("HourlyBell", 2.5), ("Countdown", 2.5), ("Jee", 1), ("Active", 1)),
                    Zone(1, ("NetWorth", 1.2), ("NetWorthGraph", 2.5), ("Accounts", 2), ("Balance", 1), ("Recent", 2.2), ("Top", 2.2)),
                    Zone(1.3, ("Songs", 1), ("Equalizer", 1), ("DailyExpenses", 1), ("MonthSummary", 0.6)),
                    Zone(1, ("Todo", 1), ("Reminders", 1), ("Notes", 1)),
                },
                Bottom = Zone(1.3, ("Habits", 1)),
            };
        }

        /// <summary>Loads the saved layout, repaired so that it holds exactly the panels in <paramref name="knownKeys"/>.</summary>
        public static DashboardLayout Load(IReadOnlyCollection<string> knownKeys)
        {
            DashboardLayout? layout = null;

            try
            {
                if (File.Exists(FilePath))
                {
                    layout = JsonSerializer.Deserialize<DashboardLayout>(File.ReadAllText(FilePath));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Unreadable layout: start again from the default arrangement.
            }

            layout ??= CreateDefault();
            layout.Repair(knownKeys);
            return layout;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not being able to remember the layout should not interrupt the app.
            }
        }

        /// <summary>
        /// Makes a loaded layout safe to use: the right number of columns, sensible sizes,
        /// no unknown or repeated panels, and any panel added since the file was saved
        /// put where the default layout has it.
        /// </summary>
        private void Repair(IReadOnlyCollection<string> knownKeys)
        {
            Columns ??= new();
            Bottom ??= new();

            while (Columns.Count < ColumnCount)
            {
                Columns.Add(new DashboardZone());
            }

            // Extra columns (from a file made with more columns) fold into the last one.
            while (Columns.Count > ColumnCount)
            {
                Columns[ColumnCount - 1].Panels.AddRange(Columns[^1].Panels);
                Columns.RemoveAt(Columns.Count - 1);
            }

            MainWeight = SaneWeight(MainWeight, 5);

            var seen = new HashSet<string>();

            foreach (var zone in AllZones)
            {
                zone.Panels ??= new();
                zone.Weight = SaneWeight(zone.Weight, 1);
                zone.Panels.RemoveAll(slot => slot == null || !knownKeys.Contains(slot.Key) || !seen.Add(slot.Key));

                foreach (var slot in zone.Panels)
                {
                    slot.Weight = SaneWeight(slot.Weight, 1);
                }
            }

            var defaults = CreateDefault();
            var defaultZones = defaults.AllZones.ToList();
            var zones = AllZones.ToList();

            for (var i = 0; i < defaultZones.Count; i++)
            {
                foreach (var slot in defaultZones[i].Panels)
                {
                    if (knownKeys.Contains(slot.Key) && seen.Add(slot.Key))
                    {
                        zones[i].Panels.Add(slot);
                    }
                }
            }

            // A panel the default layout does not mention still needs a home.
            foreach (var key in knownKeys)
            {
                if (seen.Add(key))
                {
                    Columns[0].Panels.Add(new DashboardSlot { Key = key });
                }
            }
        }

        private static double SaneWeight(double weight, double fallback) =>
            double.IsFinite(weight) && weight > 0.01 ? weight : fallback;
    }
}
