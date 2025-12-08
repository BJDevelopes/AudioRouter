using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AudioRouter.Models;
using AudioRouter.Services;

namespace AudioRouter
{
    public partial class MainWindow : Window
    {
        private readonly AudioSessionManager _sessionManager;
        private readonly AudioDeviceEnumerator _deviceEnumerator;
        private readonly RoutingManager _routingManager;
        private readonly DispatcherTimer _refreshTimer;

        private ObservableCollection<AudioSession> _audioSessions;
        private ObservableCollection<AudioDeviceInfo> _inputDevices;
        private ObservableCollection<AudioDeviceInfo> _outputDevices;
        private ObservableCollection<AudioRoute> _activeRoutes;

        private AudioSession? _selectedSession;
        private AudioDeviceInfo? _selectedInputDevice;
        private AudioDeviceInfo? _selectedOutputDevice;

        public MainWindow()
        {
            InitializeComponent();

            _sessionManager = new AudioSessionManager();
            _deviceEnumerator = new AudioDeviceEnumerator();
            _routingManager = new RoutingManager();

            _audioSessions = new ObservableCollection<AudioSession>();
            _inputDevices = new ObservableCollection<AudioDeviceInfo>();
            _outputDevices = new ObservableCollection<AudioDeviceInfo>();
            _activeRoutes = new ObservableCollection<AudioRoute>();

            AudioSessionsListBox.ItemsSource = _audioSessions;
            InputDevicesComboBox.ItemsSource = _inputDevices;
            OutputDevicesComboBox.ItemsSource = _outputDevices;
            ActiveRoutesListBox.ItemsSource = _activeRoutes;

            // Set up routing manager events
            _routingManager.OnRouteStarted += (sender, route) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus($"Started: {route}");
                    _activeRoutes.Add(route);
                });
            };

            _routingManager.OnRouteStopped += (sender, route) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus($"Stopped: {route}");
                    var existingRoute = _activeRoutes.FirstOrDefault(r => r.Id == route.Id);
                    if (existingRoute != null)
                    {
                        _activeRoutes.Remove(existingRoute);
                    }
                });
            };

            _routingManager.OnRouteError += (sender, error) =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateStatus($"Error: {error}");
                    MessageBox.Show(error, "Routing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            };

            // Set up auto-refresh timer (every 3 seconds)
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _refreshTimer.Tick += (s, e) => RefreshAudioSessions();
            _refreshTimer.Start();

            // Initial load
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshAudioSessions();
            RefreshInputDevices();
            RefreshOutputDevices();
            UpdateStatus("Ready - Select an application, input device, and output device to create a route");
        }

        private void RefreshAudioSessions()
        {
            try
            {
                var sessions = _sessionManager.GetActiveAudioSessions();

                _audioSessions.Clear();
                foreach (var session in sessions)
                {
                    _audioSessions.Add(session);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error refreshing sessions: {ex.Message}");
            }
        }

        private void RefreshInputDevices()
        {
            try
            {
                var devices = _deviceEnumerator.GetInputDevices();

                _inputDevices.Clear();
                foreach (var device in devices)
                {
                    _inputDevices.Add(device);
                }

                if (_inputDevices.Count > 0 && InputDevicesComboBox.SelectedItem == null)
                {
                    InputDevicesComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error refreshing input devices: {ex.Message}");
            }
        }

        private void RefreshOutputDevices()
        {
            try
            {
                var devices = _deviceEnumerator.GetOutputDevices();

                _outputDevices.Clear();
                foreach (var device in devices)
                {
                    _outputDevices.Add(device);
                }

                if (_outputDevices.Count > 0 && OutputDevicesComboBox.SelectedItem == null)
                {
                    OutputDevicesComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error refreshing output devices: {ex.Message}");
            }
        }

        private void AudioSessionsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _selectedSession = AudioSessionsListBox.SelectedItem as AudioSession;
            UpdateStartButtonState();
        }

        private void InputDevicesComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _selectedInputDevice = InputDevicesComboBox.SelectedItem as AudioDeviceInfo;
            UpdateStartButtonState();
        }

        private void OutputDevicesComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _selectedOutputDevice = OutputDevicesComboBox.SelectedItem as AudioDeviceInfo;
            UpdateStartButtonState();
        }

        private void UpdateStartButtonState()
        {
            StartRouteButton.IsEnabled = _selectedSession != null &&
                                        _selectedInputDevice != null &&
                                        _selectedOutputDevice != null;
        }

        private async void StartRoute_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSession == null || _selectedInputDevice == null || _selectedOutputDevice == null)
            {
                MessageBox.Show("Please select an application, input device, and output device.",
                    "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if route already exists
            var existingRoute = _activeRoutes.FirstOrDefault(r =>
                r.SourceSession.ProcessId == _selectedSession.ProcessId &&
                r.SourceDevice.Id == _selectedInputDevice.Id &&
                r.TargetDevice.Id == _selectedOutputDevice.Id);

            if (existingRoute != null)
            {
                MessageBox.Show("This route is already active.",
                    "Duplicate Route", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var route = new AudioRoute
            {
                SourceSession = _selectedSession,
                SourceDevice = _selectedInputDevice,
                TargetDevice = _selectedOutputDevice
            };

            UpdateStatus($"Starting route: {route}...");
            StartRouteButton.IsEnabled = false;

            try
            {
                var success = await _routingManager.StartRouteAsync(route);
                if (!success)
                {
                    MessageBox.Show("Failed to start audio route. The application may not be playing audio.",
                        "Route Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateStatus("Failed to start route");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting route: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus($"Error: {ex.Message}");
            }
            finally
            {
                UpdateStartButtonState();
            }
        }

        private async void StopRoute_Click(object sender, RoutedEventArgs e)
        {
            var selectedRoute = ActiveRoutesListBox.SelectedItem as AudioRoute;
            if (selectedRoute == null)
            {
                MessageBox.Show("Please select a route to stop.",
                    "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UpdateStatus($"Stopping route: {selectedRoute}...");

            try
            {
                await _routingManager.StopRouteAsync(selectedRoute.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error stopping route: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus($"Error: {ex.Message}");
            }
        }

        private void RefreshSessions_Click(object sender, RoutedEventArgs e)
        {
            RefreshAudioSessions();
            UpdateStatus("Refreshed audio sessions");
        }

        private void RefreshDevices_Click(object sender, RoutedEventArgs e)
        {
            RefreshInputDevices();
            RefreshOutputDevices();
            UpdateStatus("Refreshed input and output devices");
        }

        private void UpdateStatus(string message)
        {
            StatusTextBlock.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _refreshTimer?.Stop();

            if (_activeRoutes.Count > 0)
            {
                var result = MessageBox.Show(
                    $"There are {_activeRoutes.Count} active route(s). Stop all routes and exit?",
                    "Confirm Exit",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                UpdateStatus("Stopping all routes...");
                await _routingManager.StopAllRoutesAsync();
            }

            _routingManager?.Dispose();
            _sessionManager?.Dispose();
            _deviceEnumerator?.Dispose();
        }
    }
}
