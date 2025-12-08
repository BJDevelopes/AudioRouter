using System;
using NAudio.Wave;

namespace AudioRouter.Services
{
    /// <summary>
    /// Converts mono audio to stereo by duplicating the mono channel to both left and right
    /// </summary>
    public class MonoToStereoSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly WaveFormat _waveFormat;

        public MonoToStereoSampleProvider(ISampleProvider source)
        {
            if (source.WaveFormat.Channels != 1)
            {
                throw new ArgumentException("Source must be mono (1 channel)");
            }

            _source = source;
            _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                source.WaveFormat.SampleRate,
                2); // Convert to 2 channels (stereo)
        }

        public WaveFormat WaveFormat => _waveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            // We need half the samples from source since we're duplicating each to stereo
            var sourceSamplesNeeded = count / 2;
            var sourceBuffer = new float[sourceSamplesNeeded];
            var sourceSamplesRead = _source.Read(sourceBuffer, 0, sourceSamplesNeeded);

            // Convert mono to stereo by duplicating each sample to both channels
            var outIndex = offset;
            for (int i = 0; i < sourceSamplesRead; i++)
            {
                buffer[outIndex++] = sourceBuffer[i]; // Left channel
                buffer[outIndex++] = sourceBuffer[i]; // Right channel (same as left)
            }

            return sourceSamplesRead * 2; // We wrote twice as many samples
        }
    }
}
