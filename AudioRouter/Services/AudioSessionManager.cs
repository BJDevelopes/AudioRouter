using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using AudioRouter.Models;
using AudioRouter.Helpers;
using NAudio.CoreAudioApi;

namespace AudioRouter.Services
{
    public class AudioSessionManager
    {
        private readonly MMDeviceEnumerator _deviceEnumerator;

        public AudioSessionManager()
        {
            _deviceEnumerator = new MMDeviceEnumerator();
        }

        public List<AudioSession> GetActiveAudioSessions()
        {
            var sessions = new List<AudioSession>();
            var seenProcessIds = new HashSet<int>();

            try
            {
                // Enumerate ALL active audio devices to find all applications
                var devices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                foreach (var device in devices)
                {
                    try
                    {
                        var sessionManager = device.AudioSessionManager;

                        for (int i = 0; i < sessionManager.Sessions.Count; i++)
                        {
                            var session = sessionManager.Sessions[i];
                            var processId = (int)session.GetProcessID;

                            // Skip system sounds, already seen processes, and AudioRouter itself
                            var currentProcessId = Process.GetCurrentProcess().Id;
                            if (processId == 0 || processId == currentProcessId || seenProcessIds.Contains(processId))
                                continue;

                            try
                            {
                                var process = Process.GetProcessById(processId);
                                var displayName = session.DisplayName;

                                if (string.IsNullOrWhiteSpace(displayName))
                                {
                                    displayName = process.ProcessName;
                                }

                                // Check if process still exists and has a window
                                if (!process.HasExited)
                                {
                                    // Get executable path
                                    string? exePath = null;
                                    try
                                    {
                                        exePath = process.MainModule?.FileName;
                                    }
                                    catch { /* Access denied */ }

                                    // Check if actively playing (AudioMeterInformation peak value > 0)
                                    bool isPlaying = false;
                                    try
                                    {
                                        var peakValue = session.AudioMeterInformation.MasterPeakValue;
                                        isPlaying = peakValue > 0.001f;
                                    }
                                    catch { /* Audio meter not available */ }

                                    sessions.Add(new AudioSession
                                    {
                                        ProcessId = processId,
                                        ProcessName = process.ProcessName,
                                        DisplayName = displayName,
                                        Volume = session.SimpleAudioVolume.Volume,
                                        IsMuted = session.SimpleAudioVolume.Mute,
                                        DeviceId = device.ID,
                                        DeviceName = device.FriendlyName,
                                        ExecutablePath = exePath ?? string.Empty,
                                        Icon = IconExtractor.GetProcessIcon(processId),
                                        IsPlaying = isPlaying
                                    });

                                    seenProcessIds.Add(processId);
                                }
                            }
                            catch (Exception)
                            {
                                // Process may have exited or access denied
                                continue;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting sessions from device {device.FriendlyName}: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting audio sessions: {ex.Message}");
            }

            return sessions.OrderBy(s => s.DisplayName).ToList();
        }

        public void Dispose()
        {
            _deviceEnumerator?.Dispose();
        }
    }
}
