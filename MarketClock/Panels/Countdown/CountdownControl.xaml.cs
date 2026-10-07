using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Controls = System.Windows.Controls;
using Media = System.Windows.Media;

namespace MarketClock.Panels
{
    // Countdown panel: click a time to count down from it; a bell rings when it reaches zero.
    // It starts with buttons for 10, 15 and 20 minutes. The button at the end adds one for any other
    // time (hours, minutes, seconds), and right-clicking a button removes it. The set of buttons
    // is remembered.
    public partial class CountdownControl : System.Windows.Controls.UserControl
    {
        private static readonly int[] StandardPresets = { 10 * 60, 15 * 60, 20 * 60 }; // seconds
        private const int LongestPreset = 99 * 3600 + 59 * 60 + 59;

        private static readonly Media.Brush RunningBrush = Media.Brushes.White;

        private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
        private List<int> presets = StandardPresets.ToList(); // seconds, in the order shown
        private DateTime endsAt;

        public CountdownControl()
        {
            InitializeComponent();
            timer.Tick += (_, _) => Tick();
            BuildPresetButtons();
        }

        /// <summary>Reads the time buttons from the settings file.</summary>
        public void LoadSettings()
        {
            presets = (AppSettings.Load().CountdownButtons ?? StandardPresets.ToList())
                .Where(seconds => seconds > 0 && seconds <= LongestPreset)
                .Distinct()
                .ToList();
            BuildPresetButtons();
        }

        public void Stop()
        {
            timer.Stop();
        }

        // ---- Counting down ----

        private void Start(int seconds)
        {
            endsAt = DateTime.Now.AddSeconds(seconds);
            StopButton.Visibility = Visibility.Visible;
            CountdownText.Foreground = RunningBrush;
            ShowDisplay(true);
            SetStatus($"Ends at {endsAt:h:mm:ss tt}");

            // Only the time is shown while it counts down.
            ButtonsArea.Visibility = Visibility.Collapsed;
            CustomEditor.Visibility = Visibility.Collapsed;
            timer.Start();
            Tick();
        }

        private void Tick()
        {
            // Rounded up, so the display reaches 00:00 exactly as the bell rings.
            var remaining = TimeSpan.FromSeconds(Math.Ceiling((endsAt - DateTime.Now).TotalSeconds));

            if (remaining > TimeSpan.Zero)
            {
                CountdownText.Text = FormatClock(remaining);
                return;
            }

            timer.Stop();
            StopButton.Visibility = Visibility.Hidden;
            CountdownText.Text = "00:00";
            CountdownText.SetResourceReference(Controls.TextBlock.ForegroundProperty, Theme.AccentKey);
            SetStatus("");
            ButtonsArea.Visibility = Visibility.Visible; // back again, under the 00:00

            try
            {
                SoundPlayerHelper.PlayResourceMp3("pack://application:,,,/sounds/bell.mp3", 1.0f);
            }
            catch (Exception)
            {
                // No audio device, or the sound could not be opened: the panel still shows that time is up.
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            timer.Stop();
            StopButton.Visibility = Visibility.Hidden;
            CountdownText.Text = "";
            SetStatus("");
            ShowDisplay(false);
            ButtonsArea.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Without the time on show (nothing started, or stopped) the buttons take its place in the
        /// middle of the panel. With it, their place is under the time: they are hidden while it
        /// counts down and come back there once it has ended.
        /// </summary>
        private void ShowDisplay(bool show)
        {
            DisplayBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            Controls.Grid.SetRow(ButtonsArea, show ? 3 : 1);
        }

        /// <summary>Shows a line under the time, or hides the line when there is nothing to say.</summary>
        private void SetStatus(string text)
        {
            StatusText.Text = text;
            StatusText.Visibility = text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string FormatClock(TimeSpan time) =>
            time.TotalHours >= 1
                ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
                : $"{time.Minutes:00}:{time.Seconds:00}";

        // ---- Time buttons ----

        /// <summary>Fills the row of time buttons, with the add button at the end.</summary>
        private void BuildPresetButtons()
        {
            PresetsPanel.Children.Clear();

            foreach (var seconds in presets)
            {
                PresetsPanel.Children.Add(CreatePresetButton(seconds));
            }

            PresetsPanel.Children.Add(AddPresetButton);
        }

        private Controls.Button CreatePresetButton(int seconds)
        {
            var button = new Controls.Button
            {
                Content = FormatPreset(seconds),
                Tag = seconds,
                Style = (Style)FindResource("PanelSmallButton"),
                Margin = new Thickness(0, 0, 6, 6),
                ToolTip = "Start this countdown (right-click to remove the button)",
            };
            button.Click += Preset_Click;

            // Its own menu, so a right-click here does not open the window's menu.
            var remove = new Controls.MenuItem { Header = "Remove this button", Tag = seconds };
            remove.Click += RemovePreset_Click;
            button.ContextMenu = new Controls.ContextMenu { Items = { remove } };

            return button;
        }

        /// <summary>"10 min", "1 hr", "45 sec", or the parts that are not zero: "1h 30m", "2m 30s".</summary>
        private static string FormatPreset(int seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            var hours = (int)time.TotalHours;

            if (time.Minutes == 0 && time.Seconds == 0)
            {
                return $"{hours} hr";
            }

            if (hours == 0 && time.Seconds == 0)
            {
                return $"{time.Minutes} min";
            }

            if (hours == 0 && time.Minutes == 0)
            {
                return $"{time.Seconds} sec";
            }

            var parts = new List<string>();
            if (hours > 0) parts.Add($"{hours}h");
            if (time.Minutes > 0) parts.Add($"{time.Minutes}m");
            if (time.Seconds > 0) parts.Add($"{time.Seconds}s");
            return string.Join(" ", parts);
        }

        private void Preset_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is int seconds)
            {
                Start(seconds);
            }
        }

        private void RemovePreset_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is int seconds && presets.Remove(seconds))
            {
                BuildPresetButtons();
                SavePresets();
            }
        }

        private void SavePresets()
        {
            try
            {
                var settings = AppSettings.Load();
                settings.CountdownButtons = presets.ToList();
                settings.Save();
            }
            catch (Exception)
            {
                // Not being able to remember the buttons should not interrupt anything.
            }
        }

        // ---- Adding a custom time ----

        private void AddPreset_Click(object sender, RoutedEventArgs e)
        {
            HoursBox.Text = "";
            MinutesBox.Text = "";
            SecondsBox.Text = "";
            CustomEditor.Visibility = Visibility.Visible;
            MinutesBox.Focus();
        }

        private void CustomCancel_Click(object sender, RoutedEventArgs e)
        {
            CustomEditor.Visibility = Visibility.Collapsed;
        }

        private void CustomEditor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                AddCustomPreset();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CustomEditor.Visibility = Visibility.Collapsed;
            }
        }

        private void CustomAdd_Click(object sender, RoutedEventArgs e)
        {
            AddCustomPreset();
        }

        private void AddCustomPreset()
        {
            if (!TryReadPart(HoursBox.Text, out var hours)
                || !TryReadPart(MinutesBox.Text, out var minutes)
                || !TryReadPart(SecondsBox.Text, out var seconds))
            {
                SetStatus("Use whole numbers for hours, minutes and seconds");
                return;
            }

            var total = (long)hours * 3600 + (long)minutes * 60 + seconds;

            if (total <= 0)
            {
                SetStatus("Enter a time greater than zero");
                return;
            }

            if (total > LongestPreset)
            {
                SetStatus("That is too long (99 hours at most)");
                return;
            }

            if (presets.Contains((int)total))
            {
                SetStatus("There is already a button for that time");
                return;
            }

            presets.Add((int)total);
            BuildPresetButtons();
            SavePresets();

            CustomEditor.Visibility = Visibility.Collapsed;
            if (!timer.IsEnabled)
            {
                SetStatus("");
            }
        }

        /// <summary>An empty box counts as zero.</summary>
        private static bool TryReadPart(string text, out int value)
        {
            value = 0;
            return string.IsNullOrWhiteSpace(text) || (int.TryParse(text.Trim(), out value) && value >= 0);
        }
    }
}
