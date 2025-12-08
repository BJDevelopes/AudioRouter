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

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
