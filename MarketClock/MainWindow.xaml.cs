using System.ComponentModel;
using System.IO;
using System.Reflection;
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
        private readonly string stateFilePath = ".";

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

        public MainWindow()
        {
            InitializeComponent();
            this.Topmost = true;

            // Get path next to exe
            var exePath = Assembly.GetEntryAssembly()?.Location;
            var exeDir = Path.GetDirectoryName(exePath);
            if (exeDir != null)
            {
                stateFilePath = Path.Combine(exeDir, "windowstate.txt");
            }

            // Restore window position and size
            if (File.Exists(stateFilePath))
            {
                var lines = File.ReadAllLines(stateFilePath);
                if (lines.Length == 4 &&
                    double.TryParse(lines[0], out double left) &&
                    double.TryParse(lines[1], out double top) &&
                    double.TryParse(lines[2], out double width) &&
                    double.TryParse(lines[3], out double height))
                {
                    this.Left = left;
                    this.Top = top;
                    this.Width = width;
                    this.Height = height;
                }
            }

            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Save window position and size
            var lines = new[]
            {
                this.Left.ToString(),
                this.Top.ToString(),
                this.Width.ToString(),
                this.Height.ToString()
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
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void ExitMenu_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}