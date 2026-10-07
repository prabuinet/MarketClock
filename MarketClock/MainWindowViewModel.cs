using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;

namespace MarketClock
{    
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string currentTime = "";

        [ObservableProperty]
        private string jeeDays = "";

        [ObservableProperty]
        private string jeeHours = "";

        // Clock Related Fields
        private readonly DispatcherTimer _clockTimer = new()
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        public MainWindowViewModel()
        {
            _clockTimer.Tick += (_, _) => UpdateClock();

            UpdateClock();
            _clockTimer.Start();
        }

        private void UpdateClock()
        {
            UpdateDateTime();
            UpdateJEEHours();
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
            ComputerUsageMonitor.Stop();
        }

        [RelayCommand]
        void Loaded()
        {
            
        }
    }
}
