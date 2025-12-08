using System;

namespace AudioRouter.Models
{
    public class AudioSession
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public float Volume { get; set; }
        public bool IsMuted { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{DisplayName} ({DeviceName})";
        }
    }
}
