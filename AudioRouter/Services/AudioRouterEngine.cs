using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
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
        private readonly DispatcherTimer _levelUpdateTimer;
        private float _currentLevel = 0f;

        public event EventHandler<string>? OnError;
        public event EventHandler? OnStopped;

        public AudioRouterEngine(AudioRoute route, AudioDeviceEnumerator deviceEnumerator)
        {
            _route = route;
            _deviceEnumerator = deviceEnumerator;

            // Subscribe to volume changes
            _route.OnVolumeChanged += (sender, volume) =>
            {
                if (_volumeProvider != null && !_route.IsMuted)
                {
                    _volumeProvider.Volume = volume;
                    Debug.WriteLine($"Volume changed to {volume * 100}% for route: {_route}");
                }
            };

            // Subscribe to mute changes
            _route.OnMuteChanged += (sender, isMuted) =>
            {
                if (_volumeProvider != null)
                {
                    _volumeProvider.Volume = isMuted ? 0f : _route.Volume;
                    Debug.WriteLine($"Mute changed to {isMuted} for route: {_route}");
                }
            };

            // Set up audio level update timer (60 FPS for smooth animation)
            _levelUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _levelUpdateTimer.Tick += (s, e) =>
            {
                _route.AudioLevel = _currentLevel;
                // Decay the level for smooth falloff
                _currentLevel *= 0.85f;
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
                    Debug.WriteLine($"ERROR: Capture device not found: {_route.SourceDevice.FriendlyName}");
                    return;
                }

                // Get the target output device
                var outputDevice = _deviceEnumerator.GetDeviceById(_route.TargetDevice.Id);
                if (outputDevice == null)
                {
                    OnError?.Invoke(this, "Target output device not found");
                    Debug.WriteLine($"ERROR: Output device not found: {_route.TargetDevice.FriendlyName}");
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

                // Convert mono to stereo if needed
                ISampleProvider outputProvider = _volumeProvider;
                if (_capture.WaveFormat.Channels == 1 && outputDevice.AudioClient.MixFormat.Channels == 2)
                {
                    outputProvider = new MonoToStereoSampleProvider(_volumeProvider);
                }

                // Add diagnostic wrapper to see if output is reading from provider
                var diagnosticProvider = new DiagnosticSampleProvider(outputProvider);

                // Initialize output to target device with configured latency
                // Use Shared mode with thread-based playback for better reliability
                _output = new WasapiOut(outputDevice, AudioClientShareMode.Shared, false, _route.LatencyConfig.WasapiLatencyMilliseconds);
                _output.Init(diagnosticProvider);

                // Set up data available handler
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;

                // Boost thread priority for better audio performance
                try
                {
                    Thread.CurrentThread.Priority = ThreadPriority.Highest;
                }
                catch { /* Ignore if unable to set priority */ }

                // Start capture first to begin filling the buffer
                _capture.StartRecording();

                // Pre-buffer: wait for buffer to fill before starting playback
                await Task.Delay(200);

                // Start playback
                _output.Play();

                _isRunning = true;
                _route.IsActive = true;

                // Start audio level monitoring
                _levelUpdateTimer.Start();

                Debug.WriteLine($"✓ Route started: {_route} | Latency: {_route.LatencyConfig.Mode}");
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

                // Calculate audio level (RMS)
                float sum = 0;
                int sampleCount = e.BytesRecorded / 4; // 4 bytes per float sample
                for (int i = 0; i < e.BytesRecorded - 1; i += 4)
                {
                    float sample = BitConverter.ToSingle(e.Buffer, i);
                    sum += sample * sample;
                }

                if (sampleCount > 0)
                {
                    float rms = (float)Math.Sqrt(sum / sampleCount);
                    _currentLevel = Math.Max(_currentLevel, Math.Min(rms * 5f, 1f)); // Amplify and clamp
                }
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

                // Stop audio level monitoring
                _levelUpdateTimer.Stop();
                _route.AudioLevel = 0f;

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
                _volumeProvider = null;

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
            _levelUpdateTimer?.Stop();
        }
    }
}
