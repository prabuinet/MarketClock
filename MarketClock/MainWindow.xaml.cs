using System.ComponentModel;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace MarketClock
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private System.Windows.Point? _lastMousePosition = null;
        private const double MouseMoveThreshold = 10.0; // pixels

        private readonly string stateFilePath;
        private AccountsWindow? accountsWindow;
        private SettingsWindow? settingsWindow;

        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int WM_NCLBUTTONDOWN = 0xA1;

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOACTIVATE = 0x0010;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        public bool ScreenSaverMode { get; set; } = false;

        public MainWindow()
        {
            InitializeComponent();
            this.Topmost = true;

            stateFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MarketClock",
                "windowstate.txt");

            // Restore window position and size
            if (this.WindowState == WindowState.Normal && File.Exists(stateFilePath))
            {
                var lines = File.ReadAllLines(stateFilePath);
                if (lines.Length == 4 &&
                    double.TryParse(lines[0], CultureInfo.InvariantCulture, out double left) &&
                    double.TryParse(lines[1], CultureInfo.InvariantCulture, out double top) &&
                    double.TryParse(lines[2], CultureInfo.InvariantCulture, out double width) &&
                    double.TryParse(lines[3], CultureInfo.InvariantCulture, out double height))
                {
                    this.Left = left;
                    this.Top = top;
                    this.Width = width;
                    this.Height = height;
                }
            }

            this.Closing += MainWindow_Closing;

            InitializeDashboard();
            InitializeTrayIcon();
            LoadPanelLayout();
            LoadPlaceholderData();
            LoadNotes();
            LoadSongs();
            LoadEqualizerStyle();
            this.Closing += (_, _) => SaveNotes();
            Loaded += (_, _) => RefreshDashboard();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if(this.WindowState == WindowState.Maximized)
            {
                // Do not save state if minimized
                return;
            }

            // Save window position and size
            Directory.CreateDirectory(Path.GetDirectoryName(stateFilePath)!);

            var lines = new[]
            {
                this.Left.ToString(CultureInfo.InvariantCulture),
                this.Top.ToString(CultureInfo.InvariantCulture),
                this.Width.ToString(CultureInfo.InvariantCulture),
                this.Height.ToString(CultureInfo.InvariantCulture)
            };
            File.WriteAllLines(stateFilePath, lines);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            // Set window as topmost in Z-order
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var grip = sender as FrameworkElement;
            int hitTest = 0;
            if (grip == ResizeGripRight) hitTest = HTRIGHT;
            else if (grip == ResizeGripLeft) hitTest = HTLEFT;
            else if (grip == ResizeGripTop) hitTest = HTTOP;
            else if (grip == ResizeGripBottom) hitTest = HTBOTTOM;
            else if (grip == ResizeGripTopLeft) hitTest = HTTOPLEFT;
            else if (grip == ResizeGripTopRight) hitTest = HTTOPRIGHT;
            else if (grip == ResizeGripBottomLeft) hitTest = HTBOTTOMLEFT;
            else if (grip == ResizeGripBottomRight) hitTest = HTBOTTOMRIGHT;

            if (hitTest != 0)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                SendMessage(hwnd, WM_NCLBUTTONDOWN, hitTest, 0);
            }
        }
    
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(ScreenSaverMode)
            {
                Environment.Exit(0);
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void ExitMenu_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MaximizeMenu_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Maximized;
        }

        private void RestoreMenu_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Normal;
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Double-clicking in a text box selects a word; it should not resize the window.
            if (IsInsideTextBox(e.OriginalSource as DependencyObject))
            {
                return;
            }

            // Quick repeated clicks on the song list or the equalizer are clicks, not a resize request.
            if (SongsPanel.IsMouseOver || EqualizerPanel.IsMouseOver)
            {
                return;
            }

            if(this.WindowState == WindowState.Normal)
            {
                this.WindowState = WindowState.Maximized;
            }
            else if(this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ScreenSaverMode)
            {
                Environment.Exit(0);
            }
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!ScreenSaverMode)
                return;

            var currentPosition = e.GetPosition(this);

            if (_lastMousePosition == null)
            {
                _lastMousePosition = currentPosition;
                return;
            }

            double dx = currentPosition.X - _lastMousePosition.Value.X;
            double dy = currentPosition.Y - _lastMousePosition.Value.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance >= MouseMoveThreshold)
            {
                Environment.Exit(0);
            }
        }

        private void TestMenu_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if(this.ScreenSaverMode == false)
            {
                ComputerUsageMonitor.Start();
            }
        }

        private void ResetSessionMenu_OnClick(object sender, RoutedEventArgs e)
        {
            ComputerUsageMonitor.ResetSession();
        }

        private void StartTimer_Click(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as MainWindowViewModel;
            vm?.StartTimer(Int32.Parse(sender is FrameworkElement fe ? fe.Tag.ToString() ?? "0" : "0"));
        }

        private void StopTimer_Click(object sender, RoutedEventArgs e)
        {
            var vm = this.DataContext as MainWindowViewModel;
            vm?.StopTimer();
        }

        private void AccountsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (accountsWindow == null)
            {
                accountsWindow = new AccountsWindow
                {
                    Owner = this
                };
                accountsWindow.Closed += (_, _) => accountsWindow = null;
                accountsWindow.Closed += (_, _) => RefreshDashboard();
                accountsWindow.Show();
                return;
            }

            if (accountsWindow.WindowState == WindowState.Minimized)
            {
                accountsWindow.WindowState = WindowState.Normal;
            }

            accountsWindow.Activate();
        }

        private void SettingsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (settingsWindow == null)
            {
                settingsWindow = new SettingsWindow
                {
                    Owner = this
                };
                settingsWindow.Closed += (_, _) => settingsWindow = null;
                settingsWindow.Closed += (_, _) => RefreshDashboard();
                settingsWindow.Show();
                return;
            }

            if (settingsWindow.WindowState == WindowState.Minimized)
            {
                settingsWindow.WindowState = WindowState.Normal;
            }

            settingsWindow.Activate();
        }

        private void AddExpense_Click(object sender, RoutedEventArgs e)
        {
            var expenseWindow = new ExpenseWindow
            {
                Owner = this
            };
            expenseWindow.ShowDialog();
            RefreshDashboard();
        }

        
    }
}
