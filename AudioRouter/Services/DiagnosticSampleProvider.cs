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

            // Diagnostic logging disabled for production (enable for debugging)
            // Uncomment below to monitor audio levels
            /*
            if (_readCount++ % 100 == 0)
            {
                float maxValue = 0f;
                for (int i = offset; i < offset + samplesRead; i++)
                {
                    float absValue = Math.Abs(buffer[i]);
                    if (absValue > maxValue) maxValue = absValue;
                }
                Debug.WriteLine($"[Audio] Peak level: {maxValue:F4}");
            }
            */

            return samplesRead;
        }
    }
}
