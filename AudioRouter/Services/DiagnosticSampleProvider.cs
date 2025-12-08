using System;
using System.Diagnostics;
using NAudio.Wave;

namespace AudioRouter.Services
{
    public class DiagnosticSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private int _readCount;

        public DiagnosticSampleProvider(ISampleProvider source)
        {
            _source = source;
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var samplesRead = _source.Read(buffer, offset, count);

            // Log every 50th read to avoid spam
            if (_readCount++ % 50 == 0)
            {
                // Check if we're getting actual audio or silence
                float maxValue = 0f;
                float sumSquares = 0f;
                for (int i = offset; i < offset + samplesRead; i++)
                {
                    float absValue = Math.Abs(buffer[i]);
                    if (absValue > maxValue) maxValue = absValue;
                    sumSquares += buffer[i] * buffer[i];
                }
                float rms = samplesRead > 0 ? (float)Math.Sqrt(sumSquares / samplesRead) : 0f;

                Debug.WriteLine($"[DiagnosticSampleProvider] Read {samplesRead} samples | Peak: {maxValue:F4} | RMS: {rms:F4}");
            }

            return samplesRead;
        }
    }
}
