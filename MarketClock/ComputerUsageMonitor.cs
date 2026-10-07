using System.Media;


namespace MarketClock
{
    public static class ComputerUsageMonitor
    {
        public static event Action? ReminderTriggered;

        private static readonly TimeSpan sessionLimit = TimeSpan.FromHours(1);
        private static readonly TimeSpan idleThreshold = TimeSpan.FromMinutes(10);

        private static DateTime lastMovedTime = DateTime.Now;
        private static DateTime sessionStartTime = DateTime.Now;
        private static bool reminded = false;
        private static bool _running = false;

        public static void Start()
        {
            if (_running) return;
            _running = true;

            GlobalMouseTracker.MouseMoved += time => lastMovedTime = time;
            GlobalMouseTracker.Start();

            new Thread(MonitorLoop) { IsBackground = true }.Start();
        }

        public static void Stop() => _running = false;

        public static void ResetSession()
        {
            sessionStartTime = DateTime.Now;
            lastMovedTime = DateTime.Now;
            reminded = false;
        }

        private static void MonitorLoop()
        {
            while (_running)
            {
                var idle = DateTime.Now - lastMovedTime;

                if (idle > idleThreshold)
                {
                    sessionStartTime = DateTime.Now;
                    reminded = false;
                    System.Diagnostics.Debug.WriteLine($"User is idle, {idle.TotalSeconds} secs");
                }
                else
                {
                    var activeSession = DateTime.Now - sessionStartTime;
                    System.Diagnostics.Debug.WriteLine($"Active Session: {activeSession.TotalSeconds} secs, idle: {idle.TotalSeconds} secs");

                    if (activeSession >= sessionLimit && !reminded)
                    {
                        TriggerReminder();
                        reminded = false;
                        sessionStartTime = DateTime.Now;
                    }
                }
                                
                Thread.Sleep(1000);
            }
        }

        private static void TriggerReminder()
        {
            // Beep
            // SystemSounds.Beep.Play();
            SoundPlayerHelper.PlayResourceMp3("pack://application:,,,/sounds/notification-158187.mp3", 1.0f);

            // NotifyIcon balloon
            try
            {
                var notify = new NotifyIcon()
                {
                    Visible = true,
                    Icon = SystemIcons.Warning,
                    BalloonTipTitle = "Break Reminder",
                    BalloonTipText = "You've been using the computer for 1 hour. Please take a break.",
                };

                notify.ShowBalloonTip(5000);

                // Dispose automatically
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(20000);
                    notify.Dispose();
                });
            }
            catch { }

            ReminderTriggered?.Invoke();
        }
    }

}
