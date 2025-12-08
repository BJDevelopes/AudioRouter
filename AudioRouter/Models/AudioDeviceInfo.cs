using System;

namespace AudioRouter.Models
{
    public class AudioDeviceInfo
    {
        public string Id { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public bool IsDefault { get; set; }

        public override string ToString()
        {
            return IsDefault ? $"{FriendlyName} (Default)" : FriendlyName;
        }
    }
}
