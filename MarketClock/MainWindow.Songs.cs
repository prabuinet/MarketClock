using System.IO;
using System.Windows;
using NAudio.Wave;

namespace MarketClock
{
    // Songs panel: browses the folder chosen in Settings and plays from it.
    // Clicking a song plays that folder starting at the song; clicking a folder opens it
    // and plays everything inside it; the Play button plays the folder being shown.
    public partial class MainWindow
    {
        public sealed record SongEntry(string Glyph, string Name, string FullPath, bool IsFolder, bool IsUp);

        private static readonly HashSet<string> SongExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wma",
        };

        private readonly Random songRandom = new();

        // The song currently loaded: decoder -> spectrum tap (feeds the equalizer) -> sound card.
        private WaveOutEvent? songOutput;
        private AudioFileReader? songReader;
        private SongSpectrumTap? songTap;
        private bool songsInitialized;
        private string songLastError = "";

        private string songsRoot = "";
        private string songsCurrent = "";

        private List<string> songQueue = new();         // play order (shuffled when shuffle is on)
        private List<string> songQueueInOrder = new();  // the same songs in their folder order
        private int songQueueIndex = -1;
        private int songFailuresInARow;
        private bool songIsPlaying;
        private bool songShuffle;

        /// <summary>Reads the songs folder from settings and shows its top level.</summary>
        internal void LoadSongs()
        {
            if (!songsInitialized)
            {
                songsInitialized = true;
                Closing += (_, _) => CloseSong();
            }

            songsRoot = AppSettings.Load().SongsFolder.Trim();
            songsCurrent = songsRoot;
            ShowSongsFolder();
            UpdateSongControls();
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
                    entries.Add(new SongEntry("↑", "..", parent.FullName, true, true));
                }

                entries.AddRange(Directory.EnumerateDirectories(songsCurrent)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.CurrentCultureIgnoreCase)
                    .Select(path => new SongEntry("▸", Path.GetFileName(path), path, true, false)));

                entries.AddRange(SongsDirectlyIn(songsCurrent)
                    .Select(path => new SongEntry("♪", Path.GetFileNameWithoutExtension(path), path, false, false)));

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

        private void SongEntry_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not SongEntry entry)
            {
                return;
            }

            if (entry.IsUp)
            {
                // Going back up only browses; whatever is playing carries on.
                songsCurrent = entry.FullPath;
                ShowSongsFolder();
                return;
            }

            if (entry.IsFolder)
            {
                songsCurrent = entry.FullPath;
                ShowSongsFolder();
                PlayFolder(entry.FullPath);
                return;
            }

            // A song: play its folder, starting from the song that was clicked.
            try
            {
                StartQueue(SongsDirectlyIn(songsCurrent), entry.FullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowSongsMessage($"Could not read this folder: {ex.Message}");
            }
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
        private void PlaySongs()
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
                StartEqualizer();
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

        private void PauseSongs()
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
        private void StopSongs()
        {
            CloseSong();
            UpdateSongControls();
        }

        private void SongsNext_Click(object sender, RoutedEventArgs e)
        {
            if (songQueue.Count > 0)
            {
                PlayQueueAt(songQueueIndex + 1);
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

            for (var i = rest.Count - 1; i > 0; i--)
            {
                var j = songRandom.Next(i + 1);
                (rest[i], rest[j]) = (rest[j], rest[i]);
            }

            songQueue = current == null ? rest : rest.Prepend(current).ToList();
            songQueueIndex = 0;
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
                    StartEqualizer();
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
            SongsPlayButton.Content = songIsPlaying ? "Pause" : "Play";
            SongsShuffleButton.Content = songShuffle ? "Shuffle: on" : "Shuffle: off";
            SongsShuffleButton.Foreground = songShuffle
                ? System.Windows.Media.Brushes.Fuchsia
                : System.Windows.Media.Brushes.White;

            if (songQueueIndex >= 0 && songQueueIndex < songQueue.Count)
            {
                var name = Path.GetFileNameWithoutExtension(songQueue[songQueueIndex]);
                var state = songIsPlaying ? "Playing" : songOutput != null ? "Paused" : "Stopped";
                SongsNowPlayingText.Text = $"{state}: {name}  ({songQueueIndex + 1}/{songQueue.Count})";
            }
            else
            {
                SongsNowPlayingText.Text = "Nothing playing";
            }
        }
    }
}
