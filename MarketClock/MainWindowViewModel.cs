using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Threading;

namespace MarketClock
{    
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string currentTime = "";

        [ObservableProperty]
        private string timerCountDown = "";

        [ObservableProperty]
        private bool isTimerRunning;

        [ObservableProperty]
        private string jeeDays = "";

        [ObservableProperty]
        private string jeeHours = "";

        [ObservableProperty]
        private Visibility timerVisibility = Visibility.Collapsed;

        // Timer for Reminder
        private readonly DispatcherTimer _timer = new();
        private DateTime _timerStartTime;
        private TimeSpan _timerInterval;

        // Clock Related Fields
        private readonly DispatcherTimer _clockTimer = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        public MainWindowViewModel()
        {
            _timer.Tick += Timer_Elapsed;
            _clockTimer.Tick += (_, _) => UpdateClock();

            UpdateClock();
            _clockTimer.Start();
        }

        private void UpdateClock()
        {
            UpdateDateTime();
            UpdateJEEHours();

            TimerCountDown = IsTimerRunning ? GetRemainingString() : "00:00:00";
        }

        void UpdateJEEHours()
        {
            JeeDays = (new DateTime(2027, 1, 26) - DateTime.Today).Days.ToString();
            JeeHours = ((new DateTime(2027, 1, 26) - DateTime.Today).Days * 3).ToString();
        }

        void UpdateDateTime()
        {
            CurrentTime = DateTime.Now.ToString("dd-MM-yyyy ddd\nhh:mm:ss tt").ToUpper();
        }

        [RelayCommand]
        void Exit()
        {
            _clockTimer.Stop();
            StopTimer();
            ComputerUsageMonitor.Stop();
        }

        [RelayCommand]
        void Loaded()
        {
            
        }

        public void StartTimer(int minutes)
        {
            if (minutes <= 0)
            {
                StopTimer();
                return;
            }

            _timer.Stop();
            _timerStartTime = DateTime.Now;
            _timerInterval = TimeSpan.FromMinutes(minutes);
            _timer.Interval = _timerInterval;
            IsTimerRunning = true;
            TimerCountDown = GetRemainingString();
            _timer.Start();
        }

        private void Timer_Elapsed(object? sender, EventArgs e)
        {
            _timer.Stop();

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
