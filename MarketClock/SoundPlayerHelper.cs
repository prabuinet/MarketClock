using NAudio.Wave;
using System.IO;

namespace MarketClock
{


    public static class SoundPlayerHelper
    {
        public static void PlayResourceMp3(string resourcePath, float volume = 1.0f)
        {
            // Get stream from WPF Resource
            var resource = System.Windows.Application.GetResourceStream(
                new Uri(resourcePath, UriKind.RelativeOrAbsolute));

            if (resource == null)
                throw new FileNotFoundException("Resource not found: " + resourcePath);

            var stream = new MemoryStream();
            resource.Stream.CopyTo(stream);
            stream.Position = 0;

            var mp3Reader = new Mp3FileReader(stream);
            var volumeProvider = new WaveChannel32(mp3Reader) { Volume = volume };

            var output = new WaveOutEvent();
            output.Init(volumeProvider);
            output.Play();

            // Clean up after finish
            output.PlaybackStopped += (s, e) =>
            {
                output.Dispose();
                volumeProvider.Dispose();
                mp3Reader.Dispose();
                stream.Dispose();
            };
        }
    }

}
