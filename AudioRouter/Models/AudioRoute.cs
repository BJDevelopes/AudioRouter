using System;

namespace AudioRouter.Models
{
    public class AudioRoute
    {
        private float _volume = 1.0f;

        public Guid Id { get; set; } = Guid.NewGuid();
        public AudioSession SourceSession { get; set; } = null!;
        public AudioDeviceInfo SourceDevice { get; set; } = null!;
        public AudioDeviceInfo TargetDevice { get; set; } = null!;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0f, 1f);
                OnVolumeChanged?.Invoke(this, _volume);
            }
        }

        public event EventHandler<float>? OnVolumeChanged;

        public override string ToString()
        {
            return $"{SourceSession.DisplayName} ({SourceDevice.FriendlyName}) → {TargetDevice.FriendlyName}";
        }
    }
}
