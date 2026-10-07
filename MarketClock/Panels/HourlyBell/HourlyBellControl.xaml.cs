using System.Windows.Threading;
using Media = System.Windows.Media;

namespace MarketClock.Panels
{
    // Hourly Bell panel: counts down to the next full hour and rings the bell when it arrives.
    // It only rings between the "from" and "to" times chosen in Settings > Hourly Bell
    // (both included, 7:00 AM to 11:00 PM unless changed).
    public partial class HourlyBellControl : System.Windows.Controls.UserControl
    {
        private static readonly Media.Brush HourlyBellIdleBrush =
            new Media.SolidColorBrush(Media.Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF));

        // How the bell sounds on the hour: this many strikes, this far apart, at this volume.
        // The strikes overlap as each one rings out (the recording is about 18 seconds long).
        // 1.0 is the recording's own level; above that it is amplified.
        private const int HourlyBellRings = 1;
        private const float HourlyBellVolume = 2.5f;
        private static readonly TimeSpan HourlyBellRingGap = TimeSpan.FromSeconds(2.5);

        private DispatcherTimer? hourlyBellRingTimer;
        private int hourlyBellRingsLeft;

        private readonly DispatcherTimer hourlyBellTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
        private TimeSpan hourlyBellStart = TimeSpan.FromHours(7);
        private TimeSpan hourlyBellEnd = TimeSpan.FromHours(23);
        private DateTime nextHourlyBell;

        public HourlyBellControl()
        {
            InitializeComponent();
            hourlyBellTimer.Tick += (_, _) => UpdateHourlyBell();
        }

        /// <summary>Starts the countdown. The bell rings on the hour whether or not the panel is on show.</summary>
        public void Start()
        {
            LoadSettings();
            hourlyBellTimer.Start();
        }

        public void Stop()
        {
            hourlyBellTimer.Stop();
            hourlyBellRingTimer?.Stop();
        }

        /// <summary>Reads the bell hours from the settings file (also called after they are saved in Settings).</summary>
        public void LoadSettings()
        {
            var settings = AppSettings.Load();
            hourlyBellStart = AppSettings.ParseTimeOfDay(settings.HourlyBellStart) ?? TimeSpan.FromHours(7);
            hourlyBellEnd = AppSettings.ParseTimeOfDay(settings.HourlyBellEnd) ?? TimeSpan.FromHours(23);

            nextHourlyBell = FindNextHourlyBell(DateTime.Now);
            UpdateHourlyBell();
        }

        private bool IsHourlyBellTime(DateTime time)
        {
            var timeOfDay = time.TimeOfDay;

            // A "to" time earlier than the "from" time means the range runs past midnight.
            return hourlyBellStart <= hourlyBellEnd
                ? timeOfDay >= hourlyBellStart && timeOfDay <= hourlyBellEnd
                : timeOfDay >= hourlyBellStart || timeOfDay <= hourlyBellEnd;
        }

        /// <summary>The first full hour after <paramref name="now"/> that the bell rings at.</summary>
        private DateTime FindNextHourlyBell(DateTime now)
        {
            var hour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);

            // Some hour in the next day always qualifies unless the range holds no full hour at all.
            for (var i = 0; i < 24; i++)
            {
                if (IsHourlyBellTime(hour))
                {
                    return hour;
                }

                hour = hour.AddHours(1);
            }

            return DateTime.MaxValue;
        }

        private void UpdateHourlyBell()
        {
            var now = DateTime.Now;

            if (now >= nextHourlyBell)
            {
                // Ring only if the hour has just struck: waking the PC from sleep at 10:20
                // should not ring the 10:00 bell.
                if (now - nextHourlyBell < TimeSpan.FromSeconds(30))
                {
                    Ring();
                }

                nextHourlyBell = FindNextHourlyBell(now);
            }
            else if (nextHourlyBell != DateTime.MaxValue && nextHourlyBell - now > TimeSpan.FromHours(25))
            {
                // The system clock was moved back; work out the next bell again.
                nextHourlyBell = FindNextHourlyBell(now);
            }

            if (nextHourlyBell == DateTime.MaxValue)
            {
                HourlyBellCountdownText.Text = "--:--";
                HourlyBellCountdownText.Foreground = HourlyBellIdleBrush;
                HourlyBellNextText.Text = "No full hour in the chosen range";
                return;
            }

            // Round up so the display reaches 00:00 exactly as the bell rings.
            var remaining = TimeSpan.FromSeconds(Math.Ceiling((nextHourlyBell - now).TotalSeconds));
            var withinHours = remaining <= TimeSpan.FromHours(1);

            HourlyBellCountdownText.Text = withinHours
                ? $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}"
                : $"{(int)remaining.TotalHours}:{remaining.Minutes:00}:{remaining.Seconds:00}";
            Theme.SetAccent(HourlyBellCountdownText, System.Windows.Controls.TextBlock.ForegroundProperty, withinHours, HourlyBellIdleBrush);

            var nextText = AppSettings.FormatTimeOfDay(nextHourlyBell.TimeOfDay);
            HourlyBellNextText.Text = withinHours
                ? $"Next bell {nextText}"
                : $"Off until {nextText}";
        }

        /// <summary>Strikes the bell now, then again every <see cref="HourlyBellRingGap"/> until all the rings are done.</summary>
        public void Ring()
        {
            if (hourlyBellRingTimer == null)
            {
                hourlyBellRingTimer = new DispatcherTimer { Interval = HourlyBellRingGap };
                hourlyBellRingTimer.Tick += (_, _) => StrikeHourlyBell();
            }

            hourlyBellRingTimer.Stop();
            hourlyBellRingsLeft = HourlyBellRings;
            StrikeHourlyBell();
        }

        private void StrikeHourlyBell()
        {
            hourlyBellRingsLeft--;

            if (hourlyBellRingsLeft > 0)
            {
                hourlyBellRingTimer?.Start();
            }
            else
            {
                hourlyBellRingTimer?.Stop();
            }

            try
            {
                SoundPlayerHelper.PlayResourceMp3("pack://application:,,,/sounds/burmesebell.mp3", HourlyBellVolume);
            }
            catch (Exception)
            {
                // No audio device, or the sound could not be opened: the countdown carries on silently.
            }
        }
    }
}
