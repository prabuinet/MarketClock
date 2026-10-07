using System.IO;
using System.Windows;

namespace MarketClock.Panels
{
    // Notes & Calculator panel: free text that is saved to a file and restored on the next start.
    // A line that starts with ">" is a calculation; its answer goes on the line below as "= ..."
    // (see NotesControl.Calculator.cs).
    public partial class NotesControl : System.Windows.Controls.UserControl
    {
        private readonly string notesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarketClock",
            "notes.txt");

        private System.Windows.Threading.DispatcherTimer? notesSaveTimer;
        private bool notesLoaded;
        private bool notesDirty;

        public NotesControl()
        {
            InitializeComponent();
        }

        public void LoadNotes()
        {
            try
            {
                if (File.Exists(notesFilePath))
                {
                    NotesTextBox.Text = File.ReadAllText(notesFilePath);
                }

                notesLoaded = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Leave saving switched off, so a file that could not be read is never overwritten.
                NotesTextBox.IsReadOnly = true;
                NotesStatusText.Text = "could not load notes";
            }
        }

        private void NotesTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!notesLoaded)
            {
                return;
            }

            notesDirty = true;
            NotesStatusText.Text = "";

            // Save a moment after the typing or pasting stops, rather than on every keystroke.
            notesSaveTimer ??= CreateNotesSaveTimer();
            notesSaveTimer.Stop();
            notesSaveTimer.Start();
        }

        private System.Windows.Threading.DispatcherTimer CreateNotesSaveTimer()
        {
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => SaveNotes();
            return timer;
        }

        private void NotesTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            RefreshNoteCalculations();
            SaveNotes();
        }

        public void SaveNotes()
        {
            notesSaveTimer?.Stop();

            if (!notesLoaded || !notesDirty)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(notesFilePath)!);
                File.WriteAllText(notesFilePath, NotesTextBox.Text);
                notesDirty = false;
                NotesStatusText.Text = "saved";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                NotesStatusText.Text = "could not save notes";
            }
        }
    }
}
