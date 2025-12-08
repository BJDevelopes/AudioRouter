using System;
using System.Collections.Generic;
using System.Linq;
using AudioRouter.Models;
using NAudio.CoreAudioApi;

namespace AudioRouter.Services
{
    public class AudioDeviceEnumerator
    {
        private readonly MMDeviceEnumerator _deviceEnumerator;

        public AudioDeviceEnumerator()
        {
            _deviceEnumerator = new MMDeviceEnumerator();
        }

        public List<AudioDeviceInfo> GetInputDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                var defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var deviceCollection = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                foreach (var device in deviceCollection)
                {
                    devices.Add(new AudioDeviceInfo
                    {
                        Id = device.ID,
                        FriendlyName = device.FriendlyName,
                        IsDefault = device.ID == defaultDevice.ID
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating input devices: {ex.Message}");
            }

            return devices.OrderByDescending(d => d.IsDefault).ThenBy(d => d.FriendlyName).ToList();
        }

        public List<AudioDeviceInfo> GetOutputDevices()
        {
            var devices = new List<AudioDeviceInfo>();

            try
            {
                var defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var deviceCollection = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                foreach (var device in deviceCollection)
                {
                    devices.Add(new AudioDeviceInfo
                    {
                        Id = device.ID,
                        FriendlyName = device.FriendlyName,
                        IsDefault = device.ID == defaultDevice.ID
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enumerating output devices: {ex.Message}");
            }

            return devices.OrderByDescending(d => d.IsDefault).ThenBy(d => d.FriendlyName).ToList();
        }

        public MMDevice? GetDeviceById(string deviceId)
        {
            try
            {
                return _deviceEnumerator.GetDevice(deviceId);
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            _deviceEnumerator?.Dispose();
        }
    }
}
