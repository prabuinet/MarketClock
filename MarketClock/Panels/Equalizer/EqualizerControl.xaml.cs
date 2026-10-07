using System.Windows;

namespace MarketClock.Panels
{
    // Equalizer panel: follows the song being played, in the style of Winamp's spectrum
    // display - levels jump up with the music, fall back smoothly, and leave a "peak"
    // marker that drops more slowly. It is drawn by a GPU shader (see EqualizerShaders.cs)
    // in one of several looks; clicking the panel moves to the next look. If shaders are
    // not available it falls back to plain bars drawn on a canvas.
    public partial class EqualizerControl : System.Windows.Controls.UserControl
    {
        private const int EqualizerBarCount = EqualizerShaders.BandCount;
        private const double EqualizerLowestHz = 40;
        private const double EqualizerHighestHz = 16000;
        private const double EqualizerQuietDb = -75;  // drawn as an empty bar
        private const double EqualizerLoudDb = -15;   // drawn as a full bar

        private readonly double[] equalizerLevels = new double[EqualizerBarCount];
        private readonly double[] equalizerPeaks = new double[EqualizerBarCount];
        private readonly System.Windows.Shapes.Rectangle[] equalizerBars = new System.Windows.Shapes.Rectangle[EqualizerBarCount];
        private readonly System.Windows.Shapes.Rectangle[] equalizerPeakMarks = new System.Windows.Shapes.Rectangle[EqualizerBarCount];

        private System.Windows.Threading.DispatcherTimer? equalizerTimer;

        // Shader drawing.
        private readonly SpectrumShaderEffect?[] equalizerEffects = new SpectrumShaderEffect?[EqualizerShaders.Styles.Length];
        private readonly System.Windows.Media.Imaging.WriteableBitmap equalizerSpectrumBitmap =
            new(EqualizerBarCount, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
        private readonly byte[] equalizerSpectrumPixels = new byte[EqualizerBarCount * 4];
        private readonly DateTime equalizerStartedAt = DateTime.UtcNow;
        private System.Windows.Media.ImageBrush? equalizerSpectrumBrush;
        private SpectrumShaderEffect? equalizerEffect; // null while using the plain-bars fallback
        private int equalizerStyle;

        private SongsControl? songs; // where the music comes from

        public EqualizerControl()
        {
            InitializeComponent();
        }

        /// <summary>Makes the display follow the music played by <paramref name="songsControl"/>.</summary>
        internal void Attach(SongsControl songsControl)
        {
            songs = songsControl;
            songs.PlaybackStarted += StartEqualizer;
        }

        /// <summary>Restores the look that was last chosen.</summary>
        internal void LoadStyle()
        {
            ApplyEqualizerStyle(AppSettings.Load().EqualizerStyle);
        }

        // Clicking the equalizer moves on to the next look.
        private void Equalizer_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Handled here so the click does not also start dragging the window.
            e.Handled = true;

            ApplyEqualizerStyle(equalizerStyle + 1);

            try
            {
                var settings = AppSettings.Load();
                settings.EqualizerStyle = equalizerStyle;
                settings.Save();
            }
            catch (Exception)
            {
                // Not being able to remember the look should not interrupt anything.
            }
        }

        private void ApplyEqualizerStyle(int index)
        {
            var count = EqualizerShaders.Styles.Length;
            equalizerStyle = ((index % count) + count) % count;

            var (name, source) = EqualizerShaders.Styles[equalizerStyle];
            EqualizerStyleText.Text = $"{name} ({equalizerStyle + 1}/{count}) - click to change";

            try
            {
                if (!System.Windows.Media.RenderCapability.IsPixelShaderVersionSupported(3, 0))
                {
                    throw new NotSupportedException("This graphics setup cannot run the shaders (Shader Model 3 needed).");
                }

                equalizerSpectrumBrush ??= new System.Windows.Media.ImageBrush(equalizerSpectrumBitmap);

                var effect = equalizerEffects[equalizerStyle] ??=
                    new SpectrumShaderEffect(HlslCompiler.CompilePixelShader(source)) { Spectrum = equalizerSpectrumBrush };

                equalizerEffect = effect;
                EqualizerShaderSurface.Effect = effect;
                EqualizerShaderSurface.Visibility = Visibility.Visible;
                EqualizerCanvas.Visibility = Visibility.Collapsed;
                EqualizerErrorText.Visibility = Visibility.Collapsed;

                UpdateEqualizerShader();
            }
            catch (Exception ex)
            {
                // Show why, and keep the panel working with plain bars.
                equalizerEffect = null;
                EqualizerShaderSurface.Effect = null;
                EqualizerShaderSurface.Visibility = Visibility.Collapsed;
                EqualizerCanvas.Visibility = Visibility.Visible;
                EqualizerErrorText.Text = $"{name}: {ex.Message}";
                EqualizerErrorText.Visibility = Visibility.Visible;
            }
        }

        private void EqualizerShaderSurface_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateEqualizerShader();
        }

        /// <summary>Hands the current levels, peaks and clock to the active shader.</summary>
        private void UpdateEqualizerShader()
        {
            if (equalizerEffect == null)
            {
                return;
            }

            for (var i = 0; i < EqualizerBarCount; i++)
            {
                equalizerSpectrumPixels[i * 4 + 0] = 0;                                                       // blue: unused
                equalizerSpectrumPixels[i * 4 + 1] = (byte)Math.Round(Math.Clamp(equalizerPeaks[i], 0, 1) * 255);  // green: peak
                equalizerSpectrumPixels[i * 4 + 2] = (byte)Math.Round(Math.Clamp(equalizerLevels[i], 0, 1) * 255); // red: level
                equalizerSpectrumPixels[i * 4 + 3] = 255;
            }

            equalizerSpectrumBitmap.WritePixels(
                new Int32Rect(0, 0, EqualizerBarCount, 1), equalizerSpectrumPixels, equalizerSpectrumPixels.Length, 0);

            var height = EqualizerShaderSurface.ActualHeight;
            equalizerEffect.Aspect = height > 0 ? EqualizerShaderSurface.ActualWidth / height : 1;
            equalizerEffect.Time = (DateTime.UtcNow - equalizerStartedAt).TotalSeconds;
            equalizerEffect.Bass = (equalizerLevels[0] + equalizerLevels[1] + equalizerLevels[2] + equalizerLevels[3]) / 4;
        }

        /// <summary>Starts the animation; it stops itself once the music has stopped and the bars have settled.</summary>
        private void StartEqualizer()
        {
            if (equalizerTimer == null)
            {
                equalizerTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
                equalizerTimer.Tick += (_, _) => EqualizerTick();
            }

            equalizerTimer.Start();
        }

        private void EqualizerCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            BuildEqualizerBars();
            DrawEqualizer();
        }

        /// <summary>Lays out one bar and one peak marker per frequency band across the canvas.</summary>
        private void BuildEqualizerBars()
        {
            var width = EqualizerCanvas.ActualWidth;
            var height = EqualizerCanvas.ActualHeight;

            EqualizerCanvas.Children.Clear();

            if (width <= 0 || height <= 0)
            {
                return;
            }

            const double gap = 2;
            var barWidth = Math.Max(1, (width - gap * (EqualizerBarCount - 1)) / EqualizerBarCount);

            // Every bar is drawn full height in the same green-to-red fade and then clipped
            // to its current level, so the colour depends on how high the bar reaches.
            var fill = new System.Windows.Media.LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 1),
                EndPoint = new System.Windows.Point(0, 0),
            };
            fill.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0x39, 0xD3, 0x53), 0.0));
            fill.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0x39, 0xD3, 0x53), 0.45));
            fill.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0xFF, 0xD6, 0x0A), 0.75));
            fill.GradientStops.Add(new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromRgb(0xFF, 0x45, 0x3A), 1.0));
            fill.Freeze();

            for (var i = 0; i < EqualizerBarCount; i++)
            {
                var left = i * (barWidth + gap);

                var bar = new System.Windows.Shapes.Rectangle { Width = barWidth, Height = height, Fill = fill };
                System.Windows.Controls.Canvas.SetLeft(bar, left);
                System.Windows.Controls.Canvas.SetTop(bar, 0);
                EqualizerCanvas.Children.Add(bar);
                equalizerBars[i] = bar;

                var peak = new System.Windows.Shapes.Rectangle
                {
                    Width = barWidth,
                    Height = 2,
                    Fill = System.Windows.Media.Brushes.White,
                    Opacity = 0.8,
                };
                System.Windows.Controls.Canvas.SetLeft(peak, left);
                EqualizerCanvas.Children.Add(peak);
                equalizerPeakMarks[i] = peak;
            }
        }

        private void EqualizerTick()
        {
            var tap = songs?.SpectrumTap;
            var playing = songs?.IsPlaying == true;
            var spectrum = playing ? tap?.LatestSpectrum : null;
            var sampleRate = tap?.SampleRate ?? 44100;
            var anythingShowing = false;

            for (var i = 0; i < EqualizerBarCount; i++)
            {
                var target = spectrum == null ? 0 : EqualizerBandLevel(spectrum, sampleRate, i);

                // Jump up straight away, fall back gradually.
                equalizerLevels[i] = Math.Max(target, equalizerLevels[i] - 0.06);
                equalizerPeaks[i] = Math.Max(equalizerLevels[i], equalizerPeaks[i] - 0.015);

                anythingShowing |= equalizerPeaks[i] > 0.001;
            }

            if (equalizerEffect != null)
            {
                UpdateEqualizerShader();
            }
            else
            {
                DrawEqualizer();
            }

            if (!playing && !anythingShowing)
            {
                equalizerTimer?.Stop();
            }
        }

        /// <summary>How loud one bar's slice of frequencies is right now, from 0 (silent) to 1 (full).</summary>
        private static double EqualizerBandLevel(float[] spectrum, int sampleRate, int bar)
        {
            // Bars are spaced evenly by pitch (each covers the same musical interval), like Winamp.
            var ratio = EqualizerHighestHz / EqualizerLowestHz;
            var fromHz = EqualizerLowestHz * Math.Pow(ratio, (double)bar / EqualizerBarCount);
            var toHz = EqualizerLowestHz * Math.Pow(ratio, (double)(bar + 1) / EqualizerBarCount);

            var hzPerSlot = (double)sampleRate / SongSpectrumTap.FftLength;
            var from = Math.Clamp((int)(fromHz / hzPerSlot), 1, spectrum.Length - 1);
            var to = Math.Clamp((int)Math.Ceiling(toHz / hzPerSlot), from + 1, spectrum.Length);

            var loudest = 0f;
            for (var slot = from; slot < to; slot++)
            {
                loudest = Math.Max(loudest, spectrum[slot]);
            }

            if (loudest <= 0)
            {
                return 0;
            }

            var db = 20 * Math.Log10(loudest);
            return Math.Clamp((db - EqualizerQuietDb) / (EqualizerLoudDb - EqualizerQuietDb), 0, 1);
        }

        private void DrawEqualizer()
        {
            var height = EqualizerCanvas.ActualHeight;

            if (height <= 0 || EqualizerCanvas.Children.Count == 0)
            {
                return;
            }

            for (var i = 0; i < EqualizerBarCount; i++)
            {
                var bar = equalizerBars[i];
                var barHeight = equalizerLevels[i] * height;
                bar.Clip = new System.Windows.Media.RectangleGeometry(new Rect(0, height - barHeight, bar.Width, barHeight));

                var peakTop = Math.Clamp(height - equalizerPeaks[i] * height - 2, 0, height - 2);
                System.Windows.Controls.Canvas.SetTop(equalizerPeakMarks[i], peakTop);
            }
        }
    }
}
