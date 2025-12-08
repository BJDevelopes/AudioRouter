using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AudioRouter.Models;
using AudioRouter.Services;
using AudioRouter.Helpers;

namespace AudioRouter
{
    public partial class MainWindow : Window
    {
        private readonly AudioSessionManager _sessionManager;
        private readonly AudioDeviceEnumerator _deviceEnumerator;
        private readonly RoutingManager _routingManager;
        private readonly DispatcherTimer _refreshTimer;

        private ObservableCollection<AudioSession> _audioSessions;
        private ObservableCollection<AudioDeviceInfo> _outputDevices;
        private ObservableCollection<AudioRoute> _activeRoutes;

        private AudioSession? _selectedSession;
        private AudioDeviceInfo? _selectedOutputDevice;
        private LatencyMode _selectedLatencyMode = LatencyMode.UltraLow;

        public MainWindow()
        {
            InitializeComponent();

            _sessionManager = new AudioSessionManager();
            _deviceEnumerator = new AudioDeviceEnumerator();
            _routingManager = new RoutingManager();

            _audioSessions = new ObservableCollection<AudioSession>();
            _outputDevices = new ObservableCollection<AudioDeviceInfo>();
            _activeRoutes = new ObservableCollection<AudioRoute>();

            AudioSessionsListBox.ItemsSource = _audioSessions;
            OutputDevicesComboBox.ItemsSource = _outputDevices;
            ActiveRoutesListBox.ItemsSource = _activeRoutes;

            // Set up latency mode selector
            LatencyModeComboBox.ItemsSource = new[]
            {
                LatencyMode.UltraLow,
                LatencyMode.Low,
                LatencyMode.Normal,
                LatencyMode.High
            };
            LatencyModeComboBox.SelectedItem = LatencyMode.UltraLow;

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
            // Check admin status and update UI
            bool isAdmin = AdminHelper.IsRunningAsAdmin();
            AdminBadge.Visibility = isAdmin ? Visibility.Visible : Visibility.Collapsed;
            RestartAsAdminButton.Visibility = isAdmin ? Visibility.Collapsed : Visibility.Visible;

            // Boost process priority if running as admin
            if (isAdmin)
            {
                AdminHelper.SetProcessPriority();
                UpdateStatus("Running in ADMIN MODE with HIGH process priority");
            }
            else
            {
                UpdateStatus("Running in normal mode - Run as admin for better performance");
            }

            RefreshAudioSessions();
            RefreshOutputDevices();
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

        private void OutputDevicesComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _selectedOutputDevice = OutputDevicesComboBox.SelectedItem as AudioDeviceInfo;
            UpdateStartButtonState();
        }

        private void LatencyModeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LatencyModeComboBox.SelectedItem is LatencyMode mode)
            {
                _selectedLatencyMode = mode;
                var config = LatencyConfiguration.GetConfiguration(mode);
                LatencyInfoText.Text = $"{config} - Buffer: {config.BufferMilliseconds}ms, WASAPI: {config.WasapiLatencyMilliseconds}ms";
            }
        }

        private void RestartAsAdmin_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Audio Router will restart with administrator privileges for better performance.\n\nContinue?",
                "Restart as Administrator",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                AdminHelper.RestartAsAdmin();
            }
        }

        private void UpdateStartButtonState()
        {
            StartRouteButton.IsEnabled = _selectedSession != null &&
                                        _selectedOutputDevice != null;
        }

        private async void StartRoute_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSession == null || _selectedOutputDevice == null)
            {
                MessageBox.Show("Please select an application and an output device.",
                    "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Auto-detect the input device from the selected application
            var sourceDevice = new AudioDeviceInfo
            {
                Id = _selectedSession.DeviceId,
                FriendlyName = _selectedSession.DeviceName,
                IsDefault = false
            };

            // Check if route already exists
            var existingRoute = _activeRoutes.FirstOrDefault(r =>
                r.SourceSession.ProcessId == _selectedSession.ProcessId &&
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
                SourceDevice = sourceDevice,
                TargetDevice = _selectedOutputDevice,
                LatencyConfig = LatencyConfiguration.GetConfiguration(_selectedLatencyMode)
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
            RefreshOutputDevices();
            UpdateStatus("Refreshed output devices");
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
