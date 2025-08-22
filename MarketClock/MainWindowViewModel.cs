using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MarketClock
{    
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string currentTime = "";

        private bool running = true;

        void UpdateDateTime()
        {
            CurrentTime = System.DateTime.Now.ToString("dd-MM-yyyy ddd\nhh:mm:ss tt");
        }

        public MainWindowViewModel()
        {
            new Thread(() =>
            {
                while (running)
                {
                    UpdateDateTime();
                    Thread.Sleep(1000);
                }
            }).Start();
        }

        [RelayCommand]
        void Exit()
        {
            running = false;
        }
    }
}
