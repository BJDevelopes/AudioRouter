using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using AudioRouter.Models;
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

            try
            {
                var defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var sessionManager = defaultDevice.AudioSessionManager;

                for (int i = 0; i < sessionManager.Sessions.Count; i++)
                {
                    var session = sessionManager.Sessions[i];
                    var processId = (int)session.GetProcessID;

                    if (processId == 0) continue;

                    try
                    {
                        var process = Process.GetProcessById(processId);
                        var displayName = session.DisplayName;

                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            displayName = process.ProcessName;
                        }

                        sessions.Add(new AudioSession
                        {
                            ProcessId = processId,
                            ProcessName = process.ProcessName,
                            DisplayName = displayName,
                            Volume = session.SimpleAudioVolume.Volume,
                            IsMuted = session.SimpleAudioVolume.Mute
                        });
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
