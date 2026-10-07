namespace MarketClock.Panels
{
    // Reminders panel. Not implemented yet: it shows sample data,
    // which can be deleted once the real feature exists.
    public partial class RemindersControl : System.Windows.Controls.UserControl
    {
        public sealed record ReminderItemRow(string Title, string Date, string Due, bool IsUrgent);

        public RemindersControl()
        {
            InitializeComponent();
            RemindersList.ItemsSource = BuildSampleReminders();
        }

        private static List<ReminderItemRow> BuildSampleReminders()
        {
            var samples = new (string Title, int DaysAway)[]
            {
                ("Electricity bill", 0),
                ("Credit card payment", 2),
                ("Insurance premium", 9),
                ("Vehicle service", 16),
                ("Tax filing", 27),
            };

            return samples
                .Select(sample => new ReminderItemRow(
                    sample.Title,
                    DateTime.Today.AddDays(sample.DaysAway).ToString("ddd, dd MMM"),
                    sample.DaysAway switch
                    {
                        0 => "today",
                        1 => "tomorrow",
                        _ => $"in {sample.DaysAway} days",
                    },
                    sample.DaysAway <= 2))
                .ToList();
        }
    }
}
