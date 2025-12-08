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
                Debug.WriteLine($"[DiagnosticSampleProvider] Read {samplesRead} samples (requested {count})");
            }

            return samplesRead;
        }
    }
}
