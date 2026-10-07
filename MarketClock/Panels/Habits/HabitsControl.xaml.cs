using System.Windows;

namespace MarketClock.Panels
{
    // Habit Tracker panel. Not implemented yet: it shows sample data,
    // which can be deleted once the real feature exists.
    public partial class HabitsControl : System.Windows.Controls.UserControl
    {
        public sealed record HabitDayCell(System.Windows.Media.Brush Fill, string Tip);

        public sealed record HabitItemRow(string Name, List<HabitDayCell> Days, string Percent);

        private const double HabitNameWidth = 130;
        private const double HabitPercentWidth = 48;
        private const double HabitCellPitch = 15; // 12px cell + 3px gap

        // A day is either done (green) or not done (faint). Keep in step with the key in HabitsControl.xaml.
        private static readonly System.Windows.Media.Brush HabitNotDoneBrush = CreateBrush(0x14, 0xFF, 0xFF, 0xFF);
        private static readonly System.Windows.Media.Brush HabitDoneBrush = CreateBrush(0xFF, 0x39, 0xD3, 0x53);

        private static readonly string[] SampleHabits =
        {
            "Exercise",
            "Read 30 min",
            "Meditate",
            "Sleep by 11",
            "No sugar",
        };

        private int habitDayCount;

        public HabitsControl()
        {
            InitializeComponent();
        }

        private void HabitRowsList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Show as many days as fit, so the newest day is always at the right edge.
            var available = HabitRowsList.ActualWidth - HabitNameWidth - HabitPercentWidth;
            var dayCount = Math.Clamp((int)(available / HabitCellPitch), 7, 180);

            if (dayCount == habitDayCount)
            {
                return;
            }

            habitDayCount = dayCount;
            HabitRowsList.ItemsSource = BuildSampleHabits(dayCount);
        }

        private static List<HabitItemRow> BuildSampleHabits(int dayCount)
        {
            var today = DateTime.Today;
            var rows = new List<HabitItemRow>();

            for (var habit = 0; habit < SampleHabits.Length; habit++)
            {
                var days = new List<HabitDayCell>();
                var doneDays = 0;

                for (var offset = dayCount - 1; offset >= 0; offset--)
                {
                    var date = today.AddDays(-offset);
                    var done = SampleHabitDone(habit, date);

                    if (done)
                    {
                        doneDays++;
                    }

                    days.Add(new HabitDayCell(
                        done ? HabitDoneBrush : HabitNotDoneBrush,
                        $"{date:ddd, dd MMM}  {(done ? "done" : "not done")}"));
                }

                rows.Add(new HabitItemRow(SampleHabits[habit], days, $"{100 * doneDays / dayCount}%"));
            }

            return rows;
        }

        /// <summary>A made-up but stable done / not done for a habit on a date, so resizing does not reshuffle it.</summary>
        private static bool SampleHabitDone(int habit, DateTime date)
        {
            var seed = (uint)(date.Year * 372 + date.Month * 31 + date.Day) * 2654435761u + (uint)(habit + 1) * 40503u;
            var roll = (int)((seed >> 8) % 100);
            var skipChance = 20 + habit * 8;

            return roll >= skipChance;
        }

        private static System.Windows.Media.Brush CreateBrush(byte a, byte r, byte g, byte b)
        {
            var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
