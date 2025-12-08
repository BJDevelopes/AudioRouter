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
        private int _packetCount;

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
                Debug.WriteLine($"Attempting to get device by ID: {_route.SourceDevice.Id}");
                Debug.WriteLine($"Device name: {_route.SourceDevice.FriendlyName}");

                var captureDevice = _deviceEnumerator.GetDeviceById(_route.SourceDevice.Id);
                if (captureDevice == null)
                {
                    OnError?.Invoke(this, "Source input device not found");
                    Debug.WriteLine("ERROR: captureDevice is null!");
                    return;
                }

                Debug.WriteLine($"Capture device found: {captureDevice.FriendlyName}");
                Debug.WriteLine($"Capture device state: {captureDevice.State}");

                // Get the target output device
                Debug.WriteLine($"Attempting to get output device by ID: {_route.TargetDevice.Id}");
                Debug.WriteLine($"Output device name: {_route.TargetDevice.FriendlyName}");

                var outputDevice = _deviceEnumerator.GetDeviceById(_route.TargetDevice.Id);
                if (outputDevice == null)
                {
                    OnError?.Invoke(this, "Target output device not found");
                    Debug.WriteLine("ERROR: outputDevice is null!");
                    return;
                }

                Debug.WriteLine($"Output device found: {outputDevice.FriendlyName}");
                Debug.WriteLine($"Output device state: {outputDevice.State}");

                // Initialize loopback capture
                _capture = new WasapiLoopbackCapture(captureDevice);

                // Create a buffered wave provider with configured latency
                _waveProvider = new BufferedWaveProvider(_capture.WaveFormat)
                {
                    BufferDuration = _route.LatencyConfig.BufferDuration,
                    DiscardOnBufferOverflow = true,
                    ReadFully = false // Better for low latency
                };

                Debug.WriteLine($"Capture format: {_capture.WaveFormat}");
                Debug.WriteLine($"  Sample Rate: {_capture.WaveFormat.SampleRate}");
                Debug.WriteLine($"  Channels: {_capture.WaveFormat.Channels}");
                Debug.WriteLine($"  Bits Per Sample: {_capture.WaveFormat.BitsPerSample}");

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
                    Debug.WriteLine("Converting MONO to STEREO for compatibility");
                    outputProvider = new MonoToStereoSampleProvider(_volumeProvider);
                }
                else
                {
                    Debug.WriteLine($"No channel conversion needed: Capture={_capture.WaveFormat.Channels}ch, Output={outputDevice.AudioClient.MixFormat.Channels}ch");
                }

                // Add diagnostic wrapper to see if output is reading from provider
                var diagnosticProvider = new DiagnosticSampleProvider(outputProvider);

                // Initialize output to target device with configured latency
                // Use Shared mode with thread-based playback for better reliability
                _output = new WasapiOut(outputDevice, AudioClientShareMode.Shared, false, _route.LatencyConfig.WasapiLatencyMilliseconds);

                Debug.WriteLine($"Initializing WasapiOut with output device: {outputDevice.FriendlyName}");
                Debug.WriteLine($"Output device format: {outputDevice.AudioClient.MixFormat}");

                _output.Init(diagnosticProvider);

                Debug.WriteLine($"WasapiOut initialized successfully");
                Debug.WriteLine($"Output format after init: {_output.OutputWaveFormat}");

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

                // Start capture first to begin filling the buffer
                Debug.WriteLine("Starting capture...");
                _capture.StartRecording();

                // Pre-buffer: wait a bit for buffer to fill before starting playback
                Debug.WriteLine("Pre-buffering audio...");
                await Task.Delay(200); // Wait 200ms for buffer to fill

                Debug.WriteLine($"Buffer status before playback: {_waveProvider.BufferedBytes} bytes ({_waveProvider.BufferedDuration.TotalMilliseconds:F1}ms)");

                Debug.WriteLine("Starting playback...");
                _output.Play();

                Debug.WriteLine($"Output PlaybackState: {_output.PlaybackState}");

                _isRunning = true;
                _route.IsActive = true;

                Debug.WriteLine($"✓ Route successfully started: {_route}");
                Debug.WriteLine($"  Capture format: {_capture.WaveFormat}");
                Debug.WriteLine($"  Buffer: {_route.LatencyConfig.BufferMilliseconds}ms, WASAPI: {_route.LatencyConfig.WasapiLatencyMilliseconds}ms");
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

                // Debug every 10th packet to reduce spam
                if (_packetCount++ % 10 == 0)
                {
                    Debug.WriteLine($"Captured {e.BytesRecorded} bytes | Buffer: {_waveProvider.BufferedBytes} bytes ({_waveProvider.BufferedDuration.TotalMilliseconds:F1}ms) | Output state: {_output?.PlaybackState}");
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
