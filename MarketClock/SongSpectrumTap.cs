using NAudio.Dsp;
using NAudio.Wave;

namespace MarketClock
{
    /// <summary>
    /// Sits between the song being decoded and the sound card. Audio passes through
    /// untouched; along the way it is measured so the equalizer panel can follow the music.
    /// </summary>
    public sealed class SongSpectrumTap : ISampleProvider
    {
        public const int FftLength = 1024;
        private const int FftPower = 10; // 2^10 = 1024

        private readonly ISampleProvider source;
        private readonly Complex[] window = new Complex[FftLength];
        private int windowPosition;
        private volatile float[]? latestSpectrum;

        public SongSpectrumTap(ISampleProvider source)
        {
            this.source = source;
        }

        public WaveFormat WaveFormat => source.WaveFormat;

        /// <summary>
        /// Loudness of each frequency slot in the most recent slice of audio, lowest frequency first.
        /// Slot i covers i * SampleRate / FftLength Hz. Null until the first slice has played.
        /// </summary>
        public float[]? LatestSpectrum => latestSpectrum;

        public int SampleRate => source.WaveFormat.SampleRate;

        // Called on the audio thread.
        public int Read(Span<float> buffer)
        {
            var read = source.Read(buffer);
            var channels = source.WaveFormat.Channels;

            for (var i = 0; i + channels <= read; i += channels)
            {
                // Mix the channels down to one value per moment in time.
                var sample = 0f;
                for (var channel = 0; channel < channels; channel++)
                {
                    sample += buffer[i + channel];
                }

                sample /= channels;

                window[windowPosition].X = (float)(sample * FastFourierTransform.HammingWindow(windowPosition, FftLength));
                window[windowPosition].Y = 0;
                windowPosition++;

                if (windowPosition == FftLength)
                {
                    windowPosition = 0;
                    Analyse();
                }
            }

            return read;
        }

        private void Analyse()
        {
            var data = (Complex[])window.Clone();
            FastFourierTransform.FFT(true, FftPower, data);

            var spectrum = new float[FftLength / 2];
            for (var i = 0; i < spectrum.Length; i++)
            {
                spectrum[i] = MathF.Sqrt(data[i].X * data[i].X + data[i].Y * data[i].Y);
            }

            latestSpectrum = spectrum;
        }
    }
}
