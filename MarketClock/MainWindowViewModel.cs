using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Timers;
using System.Windows;

namespace MarketClock
{    
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string currentTime = "";

        [ObservableProperty]
        private string activeDuration = "";

        [ObservableProperty]
        private string timerCountDown = "";

        [ObservableProperty]
        private bool isTimerRunning;

        [ObservableProperty]
        private Visibility timerVisibility = Visibility.Collapsed;

        // Timer for Reminder 
        System.Timers.Timer _timer = new();
        private DateTime _timerStartTime;
        private TimeSpan _timerInterval;

        // Clock Related Fields
        private bool _clockRunning = true;

        public MainWindowViewModel()
        {
            _timer.AutoReset = false;
            _timer.Elapsed += Timer_Elapsed;

            new Thread(() =>
            {
                while (_clockRunning)
                {
                    UpdateDateTime();

                    //// Update last mouse moved time ago
                    ActiveDuration = GetTimeDiffAgo(ComputerUsageMonitor.GetActiveDuration());

                    TimerCountDown = IsTimerRunning ? GetRemainingString() : "00:00:00";

                    Thread.Sleep(1000);
                }
            }).Start();
        }

        void UpdateDateTime()
        {
            CurrentTime = DateTime.Now.ToString("dd-MM-yyyy ddd\nhh:mm:ss tt").ToUpper();
        }

        private string GetTimeDiffAgo(TimeSpan diff)
        {
            if (diff.TotalSeconds < 60)
            {
                return $"{(int)diff.TotalSeconds} seconds";
            }
            else if (diff.TotalMinutes < 60)
            {
                return $"{(int)diff.TotalMinutes} minute" + (diff.TotalMinutes > 1 ? "s" : "");
            }
            else if (diff.TotalHours < 24)
            {
                return $"{(int)diff.TotalHours} hour" + (diff.TotalHours > 1 ? "s" : "");
            }
            else
            {
                return $"{(int)diff.TotalDays} days";
            }
        }

        [RelayCommand]
        void Exit()
        {
            _clockRunning = false;
        }

        [RelayCommand]
        void Loaded()
        {
            
        }

        public void StartTimer(int minutes)
        {
            _timerStartTime = DateTime.Now;
            _timerInterval = TimeSpan.FromMinutes(minutes);
            _timer.Interval = TimeSpan.FromMinutes(minutes).TotalMilliseconds;
            _timer.Start();
            IsTimerRunning = true;
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // Play Sound
            SoundPlayerHelper.PlayResourceMp3("pack://application:,,,/sounds/bell.mp3", 1.0f);
            IsTimerRunning = false;
        }

        TimeSpan GetRemainingTime()
        {
            var elapsed = DateTime.Now - _timerStartTime;
            var remaining = _timerInterval - elapsed;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        string GetRemainingString()
        {
            return GetRemainingTime().ToString(@"hh\:mm\:ss");
        }
        partial void OnIsTimerRunningChanged(bool value)
        {
            TimerVisibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        internal void StopTimer()
        {
            IsTimerRunning = false;
            _timer.Stop();
        }
    }
}
