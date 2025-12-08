using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AudioRouter.Models
{
    public class AudioRoute : INotifyPropertyChanged
    {
        private float _volume = 1.0f;
        private float _audioLevel = 0f;
        private bool _isMuted = false;

        public Guid Id { get; set; } = Guid.NewGuid();
        public AudioSession SourceSession { get; set; } = null!;
        public AudioDeviceInfo SourceDevice { get; set; } = null!;
        public AudioDeviceInfo TargetDevice { get; set; } = null!;
        public LatencyConfiguration LatencyConfig { get; set; } = LatencyConfiguration.GetConfiguration(LatencyMode.UltraLow);
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0f, 1f);
                OnPropertyChanged();
                OnVolumeChanged?.Invoke(this, _volume);
            }
        }

        public float AudioLevel
        {
            get => _audioLevel;
            set
            {
                if (Math.Abs(_audioLevel - value) > 0.001f)
                {
                    _audioLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (_isMuted != value)
                {
                    _isMuted = value;
                    OnPropertyChanged();
                    OnMuteChanged?.Invoke(this, _isMuted);
                }
            }
        }

        public event EventHandler<float>? OnVolumeChanged;
        public event EventHandler<bool>? OnMuteChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{SourceSession.DisplayName} ({SourceDevice.FriendlyName}) → {TargetDevice.FriendlyName}";
        }
    }
}
