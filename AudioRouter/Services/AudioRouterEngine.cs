using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AudioRouter.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioRouter.Services
{
    public class AudioRouterEngine : IDisposable
    {
        private readonly AudioRoute _route;
        private readonly AudioDeviceEnumerator _deviceEnumerator;
        private WasapiLoopbackCapture? _capture;
        private WasapiOut? _output;
        private BufferedWaveProvider? _waveProvider;
        private VolumeSampleProvider? _volumeProvider;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning;

        public event EventHandler<string>? OnError;
        public event EventHandler? OnStopped;

        public AudioRouterEngine(AudioRoute route, AudioDeviceEnumerator deviceEnumerator)
        {
            _route = route;
            _deviceEnumerator = deviceEnumerator;

            // Subscribe to volume changes
            _route.OnVolumeChanged += (sender, volume) =>
            {
                if (_volumeProvider != null)
                {
                    _volumeProvider.Volume = volume;
                    Debug.WriteLine($"Volume changed to {volume * 100}% for route: {_route}");
                }
            };
        }

        public async Task StartAsync()
        {
            if (_isRunning) return;

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();

                // Get the source audio device to capture from
                var captureDevice = _deviceEnumerator.GetDeviceById(_route.SourceDevice.Id);
                if (captureDevice == null)
                {
                    OnError?.Invoke(this, "Source input device not found");
                    return;
                }

                // Get the target output device
                var outputDevice = _deviceEnumerator.GetDeviceById(_route.TargetDevice.Id);
                if (outputDevice == null)
                {
                    OnError?.Invoke(this, "Target output device not found");
                    return;
                }

                // Initialize loopback capture
                _capture = new WasapiLoopbackCapture(captureDevice);

                // Create a buffered wave provider with configured latency
                _waveProvider = new BufferedWaveProvider(_capture.WaveFormat)
                {
                    BufferDuration = _route.LatencyConfig.BufferDuration,
                    DiscardOnBufferOverflow = true,
                    ReadFully = false // Better for low latency
                };

                // Add volume control
                var sampleProvider = _waveProvider.ToSampleProvider();
                _volumeProvider = new VolumeSampleProvider(sampleProvider)
                {
                    Volume = _route.Volume
                };

                // Initialize output to target device with configured latency
                _output = new WasapiOut(outputDevice, AudioClientShareMode.Shared, false, _route.LatencyConfig.WasapiLatencyMilliseconds);
                _output.Init(_volumeProvider);

                Debug.WriteLine($"Route started with latency: {_route.LatencyConfig.Mode} (Buffer: {_route.LatencyConfig.BufferMilliseconds}ms, WASAPI: {_route.LatencyConfig.WasapiLatencyMilliseconds}ms)");

                // Set up data available handler
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;

                // Boost thread priority for better audio performance
                try
                {
                    Thread.CurrentThread.Priority = ThreadPriority.Highest;
                    Debug.WriteLine("Audio thread priority set to Highest");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not set thread priority: {ex.Message}");
                }

                // Start capture and playback
                _capture.StartRecording();
                _output.Play();

                _isRunning = true;
                _route.IsActive = true;

                Debug.WriteLine($"Started routing: {_route}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting audio route: {ex.Message}");
                OnError?.Invoke(this, $"Failed to start: {ex.Message}");
                await StopAsync();
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_waveProvider != null && e.BytesRecorded > 0)
            {
                _waveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                Debug.WriteLine($"Recording stopped with error: {e.Exception.Message}");
                OnError?.Invoke(this, $"Recording error: {e.Exception.Message}");
            }

            Task.Run(async () => await StopAsync());
        }

        public async Task StopAsync()
        {
            if (!_isRunning) return;

            try
            {
                _isRunning = false;
                _route.IsActive = false;

                _cancellationTokenSource?.Cancel();

                if (_capture != null)
                {
                    _capture.DataAvailable -= OnDataAvailable;
                    _capture.RecordingStopped -= OnRecordingStopped;
                    _capture.StopRecording();
                }

                _output?.Stop();

                await Task.Delay(100); // Give time for cleanup

                _capture?.Dispose();
                _output?.Dispose();
                _waveProvider = null;

                Debug.WriteLine($"Stopped routing: {_route}");
                OnStopped?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping audio route: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Task.Run(async () => await StopAsync()).Wait();
            _cancellationTokenSource?.Dispose();
        }
    }
}
