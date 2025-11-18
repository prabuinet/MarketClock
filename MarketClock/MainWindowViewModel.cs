using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MarketClock
{    
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string currentTime = "";

        [ObservableProperty]
        private string lastMouseMoved = "";

        DateTime lastMovedTime = DateTime.Now;

        private bool running = true;

        void UpdateDateTime()
        {
            CurrentTime = DateTime.Now.ToString("dd-MM-yyyy ddd\nhh:mm:ss tt").ToUpper();
        }

        private string GetTimeDiffAgo(DateTime pastTime)
        {
            TimeSpan diff = DateTime.Now - pastTime;
            if (diff.TotalSeconds < 60)
            {
                return $"{(int)diff.TotalSeconds} seconds ago";
            }
            else if (diff.TotalMinutes < 60)
            {
                return $"{(int)diff.TotalMinutes} minutes ago";
            }
            else if (diff.TotalHours < 24)
            {
                return $"{(int)diff.TotalHours} hours ago";
            }
            else
            {
                return $"{(int)diff.TotalDays} days ago";
            }
        }

        public MainWindowViewModel()
        {
            new Thread(() =>
            {
                while (running)
                {
                    UpdateDateTime();

                    //// Update last mouse moved time ago
                    //LastMouseMoved = GetTimeDiffAgo(lastMovedTime);
                    Thread.Sleep(1000);
                }
            }).Start();
        }

        [RelayCommand]
        void Exit()
        {
            running = false;
        }

        [RelayCommand]
        void Loaded()
        {
            
        }
    }
}
