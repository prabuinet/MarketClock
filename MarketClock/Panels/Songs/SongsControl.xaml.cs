using System.IO;
using System.Text.Json;
using System.Windows;
using MahApps.Metro.IconPacks;
using NAudio.Wave;

namespace MarketClock.Panels
{
    // Songs panel: plays songs from the folder chosen in Settings. It has two views, picked
    // with the icons at its top right:
    //   - Now playing (shown first): the song, a progress bar that can be clicked or dragged,
    //     the time left, the controls and the queue, with the current song highlighted.
    //   - Browse: the folder. Clicking a folder opens it; nothing plays until the play or
    //     queue icon on a line is clicked (on the ".." line they act on the folder being shown).
    // The queue is saved to a file whenever it changes and put back on the next start, without
    // starting to play.
    public partial class SongsControl : System.Windows.Controls.UserControl
    {
        public sealed record SongEntry(PackIconMaterialKind Icon, string Name, string FullPath, bool IsFolder, bool IsUp)
        {
            public string PlayTip => IsUp ? "Play this folder" : "Play";

            public string QueueTip => IsUp ? "Add this folder to the queue" : "Add to the queue";
        }

        /// <summary>The queue as it is kept in songqueue.json between runs.</summary>
        private sealed class SavedQueue
        {
            public List<string> Songs { get; set; } = new();     // play order

            public List<string> InOrder { get; set; } = new();   // the same songs before shuffling

            public int Index { get; set; } = -1;                 // the current song

            public bool Shuffle { get; set; }
        }

        private static readonly string QueueFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarketClock",
            "songqueue.json");

        /// <summary>One line of the queue in the Now playing view.</summary>
        public sealed record QueueEntry(string Name, bool IsCurrent, int Index);

        private static readonly HashSet<string> SongExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wma",
        };

        private readonly Random songRandom = new();

        // The song currently loaded: decoder -> spectrum tap (feeds the equalizer) -> sound card.
        private WaveOutEvent? songOutput;
        private AudioFileReader? songReader;
        private SongSpectrumTap? songTap;
        private string songLastError = "";

        private string songsRoot = "";
        private string songsCurrent = "";

        private List<string> songQueue = new();         // play order (shuffled when shuffle is on)
        private List<string> songQueueInOrder = new();  // the same songs in their folder order
        private int songQueueIndex = -1;
        private int songFailuresInARow;
        private bool songIsPlaying;
        private bool songShuffle;

        private readonly System.Windows.Threading.DispatcherTimer songProgressTimer =
            new() { Interval = TimeSpan.FromMilliseconds(250) };
        private bool songSeeking;     // true while the progress bar is being dragged
        private bool songQueueRestored; // saving waits for this, so an empty queue never overwrites the saved one at startup
        private (List<string>? Queue, int Count, int Index) shownQueue; // what the queue list was last built from

        public SongsControl()
        {
            InitializeComponent();
            songProgressTimer.Tick += (_, _) => UpdateSongProgress();
            ShowSongsView(browse: false);
        }

        /// <summary>Raised whenever a song starts or resumes (the equalizer starts moving on this).</summary>
        internal event Action? PlaybackStarted;

        internal bool IsPlaying => songIsPlaying;

        /// <summary>True while a song is loaded, whether it is playing or paused.</summary>
        internal bool HasSong => songOutput != null;

        /// <summary>The measurements of the song being played, which the equalizer draws.</summary>
        internal SongSpectrumTap? SpectrumTap => songTap;

        /// <summary>Reads the songs folder from settings and shows its top level.</summary>
        internal void LoadSongs()
        {
            songsRoot = AppSettings.Load().SongsFolder.Trim();
            songsCurrent = songsRoot;
            ShowSongsFolder();
            UpdateSongControls();
        }

        // ---- Keeping the queue between runs ----

        /// <summary>
        /// Puts back the queue, current song and shuffle choice from the last run (called once at startup).
        /// Nothing starts playing: the current song is shown as stopped, and Play starts it from its beginning.
        /// </summary>
        internal void RestoreQueue()
        {
            try
            {
                if (File.Exists(QueueFilePath))
                {
                    var saved = JsonSerializer.Deserialize<SavedQueue>(File.ReadAllText(QueueFilePath));

                    if (saved?.Songs is { Count: > 0 })
                    {
                        songQueue = saved.Songs;
                        songQueueInOrder = saved.InOrder is { Count: > 0 } ? saved.InOrder : saved.Songs.ToList();
                        songQueueIndex = Math.Clamp(saved.Index, 0, songQueue.Count - 1);
                    }

                    songShuffle = saved?.Shuffle ?? false;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Unreadable file: start with an empty queue.
            }

            songQueueRestored = true;
            UpdateSongControls();
        }

        private void SaveQueue()
        {
            if (!songQueueRestored)
            {
                return;
            }

            try
            {
                var saved = new SavedQueue
                {
                    Songs = songQueue,
                    InOrder = songQueueInOrder,
                    Index = songQueueIndex,
                    Shuffle = songShuffle,
                };

                Directory.CreateDirectory(Path.GetDirectoryName(QueueFilePath)!);
                File.WriteAllText(QueueFilePath, JsonSerializer.Serialize(saved));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not being able to remember the queue should not interrupt the music.
            }
        }

        // ---- Browsing ----

        private void ShowSongsFolder()
        {
            SongsList.ItemsSource = null;
            SongsPathText.Text = songsCurrent;

            if (string.IsNullOrWhiteSpace(songsRoot))
            {
                ShowSongsMessage("No songs folder set. Choose one in Settings.");
                return;
            }

            if (!Directory.Exists(songsCurrent))
            {
                ShowSongsMessage($"Folder not found: {songsCurrent}. Change it in Settings.");
                return;
            }

            try
            {
                var entries = new List<SongEntry>();

                // Going up is offered everywhere except at the configured folder itself.
                var isRoot = string.Equals(
                    Path.GetFullPath(songsCurrent).TrimEnd('\\', '/'),
                    Path.GetFullPath(songsRoot).TrimEnd('\\', '/'),
                    StringComparison.OrdinalIgnoreCase);
                var parent = Directory.GetParent(songsCurrent.TrimEnd('\\', '/'));

                if (!isRoot && parent != null)
                {
                    entries.Add(new SongEntry(PackIconMaterialKind.ArrowUp, "..", parent.FullName, true, true));
                }

                entries.AddRange(Directory.EnumerateDirectories(songsCurrent)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                    .Select(path => new SongEntry(PackIconMaterialKind.Folder, Path.GetFileName(path), path, true, false)));

                entries.AddRange(SongsDirectlyIn(songsCurrent)
                    .Select(path => new SongEntry(PackIconMaterialKind.MusicNote, Path.GetFileNameWithoutExtension(path), path, false, false)));

                SongsList.ItemsSource = entries;

                if (entries.Count == 0)
                {
                    ShowSongsMessage("No songs or folders here.");
                }
                else
                {
                    SongsMessageText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowSongsMessage($"Could not read this folder: {ex.Message}");
            }
        }

        private static List<string> SongsDirectlyIn(string folder)
        {
            return Directory.EnumerateFiles(folder)
                .Where(path => SongExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>Every song in the folder and its sub-folders, in path order.</summary>
        private static List<string> SongsUnder(string folder)
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };

            return Directory.EnumerateFiles(folder, "*", options)
                .Where(path => SongExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        private void ShowSongsMessage(string message)
        {
            SongsMessageText.Text = message;
            SongsMessageText.Visibility = Visibility.Visible;
        }

        // ---- Clicks ----

        private void SongsBrowseTab_Click(object sender, RoutedEventArgs e)
        {
            // Read the folder again, in case files were added since it was last shown.
            ShowSongsFolder();
            ShowSongsView(browse: true);
        }

        private void SongsNowPlayingTab_Click(object sender, RoutedEventArgs e)
        {
            ShowSongsView(browse: false);
        }

        /// <summary>Switches between the Browse view and the Now playing view.</summary>
        private void ShowSongsView(bool browse)
        {
            SongsBrowseView.Visibility = browse ? Visibility.Visible : Visibility.Collapsed;
            SongsNowPlayingView.Visibility = browse ? Visibility.Collapsed : Visibility.Visible;

            if (!browse)
            {
                ScrollQueueToCurrent();
            }

            SongsBrowseTabButton.Foreground = browse ? System.Windows.Media.Brushes.Fuchsia : System.Windows.Media.Brushes.White;
            SongsNowPlayingTabButton.Foreground = browse ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Fuchsia;
        }

        // Clicking a line only browses: a folder (or "..") is opened, a song does nothing.
        private void SongEntry_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is SongEntry { IsFolder: true } entry)
            {
                songsCurrent = entry.FullPath;
                ShowSongsFolder();
            }
        }

        // The play icon on a line.
        private void SongEntryPlay_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not SongEntry entry)
            {
                return;
            }

            if (entry.IsFolder)
            {
                // Everything in the folder, sub-folders included.
                PlayFolder(FolderOf(entry));
                return;
            }

            // A song: play its folder, starting from that song.
            try
            {
                StartQueue(SongsDirectlyIn(songsCurrent), entry.FullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowSongsMessage($"Could not read this folder: {ex.Message}");
            }
        }

        // The queue icon on a line: adds the song, or every song in the folder, after what is already queued.
        private async void SongEntryQueue_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not SongEntry entry)
            {
                return;
            }

            if (!entry.IsFolder)
            {
                Enqueue(new List<string> { entry.FullPath });
                return;
            }

            try
            {
                // A big folder can take a moment to walk, so do it off the UI thread.
                var folder = FolderOf(entry);
                var songs = await Task.Run(() => SongsUnder(folder));

                if (songs.Count == 0)
                {
                    ShowSongsMessage("No songs in this folder.");
                    return;
                }

                Enqueue(songs);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowSongsMessage($"Could not read this folder: {ex.Message}");
            }
        }

        /// <summary>The folder a line's play and queue icons act on: on the ".." line, the folder being shown.</summary>
        private string FolderOf(SongEntry entry) => entry.IsUp ? songsCurrent : entry.FullPath;

        /// <summary>Adds songs to the end of the queue; if nothing is queued yet, starts playing them.</summary>
        private void Enqueue(List<string> songs)
        {
            if (songQueue.Count == 0 || songQueueIndex < 0)
            {
                StartQueue(songs, null);
                return;
            }

            var added = songs.ToList();
            if (songShuffle)
            {
                Shuffle(added);
            }

            songQueueInOrder = songQueueInOrder.Concat(songs).ToList();
            songQueue.AddRange(added);
            UpdateSongControls();
        }

        private void SongsPlay_Click(object sender, RoutedEventArgs e)
        {
            if (songIsPlaying)
            {
                PauseSongs();
            }
            else
            {
                PlaySongs();
            }
        }

        /// <summary>Resumes a paused song, restarts a stopped one, or starts the folder being shown.</summary>
        internal void PlaySongs()
        {
            if (songIsPlaying)
            {
                return;
            }

            if (songOutput != null)
            {
                // Paused: carry on from where it was.
                songOutput.Play();
                songIsPlaying = true;
                PlaybackStarted?.Invoke();
                UpdateSongControls();
            }
            else if (songQueueIndex >= 0 && songQueueIndex < songQueue.Count)
            {
                // Stopped: start the same song again from the beginning.
                PlayQueueAt(songQueueIndex);
            }
            else
            {
                PlayFolder(songsCurrent);
            }
        }

        internal void PauseSongs()
        {
            if (!songIsPlaying)
            {
                return;
            }

            songOutput?.Pause();
            songIsPlaying = false;
            UpdateSongControls();
        }

        /// <summary>Stops playback. The queue is kept, so Play starts the same song from the beginning.</summary>
        internal void StopSongs()
        {
            CloseSong();
            UpdateSongControls();
        }

        private void SongsStop_Click(object sender, RoutedEventArgs e)
        {
            StopSongs();
        }

        private void SongsNext_Click(object sender, RoutedEventArgs e)
        {
            if (songQueue.Count > 0)
            {
                PlayQueueAt(songQueueIndex + 1);
            }
        }

        // Clicking a song in the queue plays it.
        private void QueueRow_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Handled here so the click does not also start dragging the window.
            e.Handled = true;

            if ((sender as FrameworkElement)?.DataContext is QueueEntry entry && entry.Index < songQueue.Count)
            {
                songFailuresInARow = 0;
                PlayQueueAt(entry.Index);
            }
        }

        // Stops playing and empties the queue.
        private void SongsClear_Click(object sender, RoutedEventArgs e)
        {
            CloseSong();
            songQueue = new();
            songQueueInOrder = new();
            songQueueIndex = -1;
            UpdateSongControls();
        }

        private void SongsPrevious_Click(object sender, RoutedEventArgs e)
        {
            if (songQueue.Count == 0)
            {
                return;
            }

            // Well into a song, Previous goes back to its start; near the start, to the song before.
            if (songReader != null && songReader.CurrentTime > TimeSpan.FromSeconds(3))
            {
                SeekSong(TimeSpan.Zero);
            }
            else
            {
                PlayQueueAt(songQueueIndex - 1);
            }
        }

        private void SongsShuffle_Click(object sender, RoutedEventArgs e)
        {
            songShuffle = !songShuffle;

            if (songQueue.Count > 0)
            {
                // Re-order what is queued, keeping the current song where it is.
                var current = songQueueIndex >= 0 ? songQueue[songQueueIndex] : null;
                BuildQueue(songQueueInOrder, current);
            }

            UpdateSongControls();
            SaveQueue();
        }

        // ---- Playback ----

        /// <summary>Plays every song in the folder, sub-folders included.</summary>
        private async void PlayFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                return;
            }

            try
            {
                // A big library can take a moment to walk, so do it off the UI thread.
                var songs = await Task.Run(() => SongsUnder(folder));

                if (songs.Count == 0)
                {
                    ShowSongsMessage("No songs in this folder.");
                    return;
                }

                StartQueue(songs, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowSongsMessage($"Could not read this folder: {ex.Message}");
            }
        }

        private void StartQueue(List<string> songs, string? startWith)
        {
            if (songs.Count == 0)
            {
                return;
            }

            BuildQueue(songs, startWith);
            songFailuresInARow = 0;
            PlayQueueAt(songQueueIndex);
        }

        /// <summary>Sets the play order for <paramref name="songs"/> and points the index at <paramref name="current"/>.</summary>
        private void BuildQueue(List<string> songs, string? current)
        {
            songQueueInOrder = songs;

            if (!songShuffle)
            {
                songQueue = songs.ToList();
                songQueueIndex = current == null ? 0 : Math.Max(0, songQueue.IndexOf(current));
                return;
            }

            // Shuffled: the current song (if any) goes first, the rest follow in random order.
            var rest = songs.Where(song => song != current).ToList();

            Shuffle(rest);

            songQueue = current == null ? rest : rest.Prepend(current).ToList();
            songQueueIndex = 0;
        }

        private void Shuffle(List<string> songs)
        {
            for (var i = songs.Count - 1; i > 0; i--)
            {
                var j = songRandom.Next(i + 1);
                (songs[i], songs[j]) = (songs[j], songs[i]);
            }
        }

        /// <summary>Plays the queued song at <paramref name="index"/>, moving on past any file that will not open.</summary>
        private void PlayQueueAt(int index)
        {
            CloseSong();

            while (songQueue.Count > 0 && songFailuresInARow < songQueue.Count)
            {
                // Wraps round, so the queue repeats when it reaches the end.
                songQueueIndex = ((index % songQueue.Count) + songQueue.Count) % songQueue.Count;

                try
                {
                    songReader = new AudioFileReader(songQueue[songQueueIndex]);
                    songTap = new SongSpectrumTap(songReader);

                    songOutput = new WaveOutEvent();
                    songOutput.PlaybackStopped += SongOutput_PlaybackStopped;
                    songOutput.Init(songTap);
                    songOutput.Play();

                    songFailuresInARow = 0;
                    songIsPlaying = true;
                    PlaybackStarted?.Invoke();
                    UpdateSongControls();
                    return;
                }
                catch (Exception ex)
                {
                    // Not a format this machine can decode, or the file has gone: say why, then try the next one.
                    songLastError = $"{Path.GetFileName(songQueue[songQueueIndex])}: {ex.GetType().Name}: {ex.Message}";
                    ShowSongsMessage($"Skipped {songLastError}");

                    CloseSong();
                    songFailuresInARow++;
                    index = songQueueIndex + 1;
                }
            }

            // Nothing in the queue would play.
            songFailuresInARow = 0;
            songQueueIndex = -1;
            songIsPlaying = false;
            UpdateSongControls();

            if (songQueue.Count > 0)
            {
                ShowSongsMessage($"None of these songs could be played. Last error - {songLastError}");
            }
        }

        // Raised when a song reaches its end (or the sound device fails).
        private void SongOutput_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            // Ignore a late notification from a song that has already been replaced.
            if (!ReferenceEquals(sender, songOutput))
            {
                return;
            }

            if (e.Exception != null)
            {
                songFailuresInARow++;
                songLastError = $"{e.Exception.GetType().Name}: {e.Exception.Message}";
                ShowSongsMessage($"Playback stopped with an error - {songLastError}");
            }

            PlayQueueAt(songQueueIndex + 1);
        }

        /// <summary>Stops and releases the current song, if there is one.</summary>
        private void CloseSong()
        {
            var output = songOutput;
            songOutput = null;
            songTap = null;
            songIsPlaying = false;

            if (output != null)
            {
                output.PlaybackStopped -= SongOutput_PlaybackStopped;
                output.Stop();
                output.Dispose();
            }

            songReader?.Dispose();
            songReader = null;
        }

        private void UpdateSongControls()
        {
            SongsPlayIcon.Kind = songIsPlaying ? PackIconMaterialKind.Pause : PackIconMaterialKind.Play;
            SongsPlayButton.ToolTip = songIsPlaying ? "Pause" : "Play";
            SongsShuffleButton.ToolTip = songShuffle ? "Shuffle is on" : "Shuffle is off";
            SongsShuffleButton.Foreground = songShuffle
                ? System.Windows.Media.Brushes.Fuchsia
                : System.Windows.Media.Brushes.White;

            var hasCurrent = songQueueIndex >= 0 && songQueueIndex < songQueue.Count;

            if (hasCurrent)
            {
                var state = songIsPlaying ? "Playing" : songOutput != null ? "Paused" : "Stopped";
                SongsStateText.Text = $"{state}  ·  {songQueueIndex + 1} of {songQueue.Count}";
            }
            else
            {
                SongsStateText.Text = "Nothing playing. Pick something in the folder view, or press Play";
            }

            // The queue list is rebuilt only when the queue or the current song has changed,
            // so pausing or resuming does not move a list that has been scrolled.
            var queueNow = (songQueue, songQueue.Count, hasCurrent ? songQueueIndex : -1);
            if (!ReferenceEquals(queueNow.Item1, shownQueue.Queue) || queueNow.Item2 != shownQueue.Count || queueNow.Item3 != shownQueue.Index)
            {
                shownQueue = queueNow;
                SaveQueue();

                var current = queueNow.Item3;
                SongsQueueList.ItemsSource = songQueue
                    .Select((song, index) => new QueueEntry(Path.GetFileNameWithoutExtension(song), index == current, index))
                    .ToList();
                ScrollQueueToCurrent();
            }

            // The progress bar only needs to keep moving while a song is playing.
            UpdateSongProgress();
            if (songIsPlaying)
            {
                songProgressTimer.Start();
            }
            else
            {
                songProgressTimer.Stop();
            }
        }

        /// <summary>Scrolls the queue list so the current song is in view, with one song above it.</summary>
        private void ScrollQueueToCurrent()
        {
            // Done once the list has been laid out; before that there is nothing to scroll.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                SongsQueueList.ApplyTemplate();

                if (shownQueue.Index >= 0
                    && SongsQueueList.Template.FindName("QueueScrollViewer", SongsQueueList) is System.Windows.Controls.ScrollViewer scroll)
                {
                    // The list scrolls by whole lines, so the offset is a line number.
                    scroll.ScrollToVerticalOffset(Math.Max(0, shownQueue.Index - 1));
                }
            }));
        }

        // ---- Progress bar ----

        /// <summary>Moves the progress bar and the time-left figure to where the song is now.</summary>
        private void UpdateSongProgress()
        {
            var reader = songReader;

            if (reader == null || reader.TotalTime <= TimeSpan.Zero)
            {
                SongProgressFill.Width = 0;
                SongsRemainingText.Text = "00:00";
                return;
            }

            var fraction = Math.Clamp(reader.CurrentTime.TotalSeconds / reader.TotalTime.TotalSeconds, 0, 1);
            SongProgressFill.Width = fraction * SongProgressArea.ActualWidth;

            var remaining = reader.TotalTime - reader.CurrentTime;
            if (remaining < TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
            }

            SongsRemainingText.Text = "-" + remaining.ToString(remaining.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");
        }

        private void SongProgressArea_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSongProgress();
        }

        private void SongProgressArea_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Handled here so the press does not also start dragging the window.
            e.Handled = true;

            if (songReader == null)
            {
                return;
            }

            songSeeking = true;
            SongProgressArea.CaptureMouse();
            SeekSongToMouse(e);
        }

        private void SongProgressArea_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (songSeeking)
            {
                SeekSongToMouse(e);
            }
        }

        private void SongProgressArea_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (songSeeking)
            {
                songSeeking = false;
                SongProgressArea.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void SeekSongToMouse(System.Windows.Input.MouseEventArgs e)
        {
            var reader = songReader;
            var width = SongProgressArea.ActualWidth;

            if (reader == null || width <= 0)
            {
                return;
            }

            var fraction = Math.Clamp(e.GetPosition(SongProgressArea).X / width, 0, 1);
            SeekSong(TimeSpan.FromSeconds(fraction * reader.TotalTime.TotalSeconds));
        }

        /// <summary>Jumps to a position in the current song.</summary>
        private void SeekSong(TimeSpan position)
        {
            var reader = songReader;
            if (reader == null)
            {
                return;
            }

            // Stop a little short of the end, so the song still finishes by itself and the next one follows.
            var latest = reader.TotalTime - TimeSpan.FromSeconds(0.5);
            if (position > latest)
            {
                position = latest;
            }

            if (position < TimeSpan.Zero)
            {
                position = TimeSpan.Zero;
            }

            try
            {
                reader.CurrentTime = position;
            }
            catch (Exception)
            {
                // Some files cannot be repositioned; the song just carries on from where it was.
            }

            UpdateSongProgress();
        }
    }
}
