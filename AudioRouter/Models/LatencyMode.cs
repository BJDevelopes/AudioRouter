namespace AudioRouter.Models
{
    public enum LatencyMode
    {
        UltraLow,    // 10-20ms - Best for movies/gaming
        Low,         // 30-50ms - Good balance
        Normal,      // 50-100ms - Stable
        High         // 100-200ms - Maximum stability
    }

    public class LatencyConfiguration
    {
        public LatencyMode Mode { get; set; }
        public int BufferMilliseconds { get; set; }
        public int WasapiLatencyMilliseconds { get; set; }
        public TimeSpan BufferDuration => TimeSpan.FromMilliseconds(BufferMilliseconds);

        public static LatencyConfiguration GetConfiguration(LatencyMode mode)
        {
            return mode switch
            {
                LatencyMode.UltraLow => new LatencyConfiguration
                {
                    Mode = LatencyMode.UltraLow,
                    BufferMilliseconds = 100,
                    WasapiLatencyMilliseconds = 10
                },
                LatencyMode.Low => new LatencyConfiguration
                {
                    Mode = LatencyMode.Low,
                    BufferMilliseconds = 200,
                    WasapiLatencyMilliseconds = 20
                },
                LatencyMode.Normal => new LatencyConfiguration
                {
                    Mode = LatencyMode.Normal,
                    BufferMilliseconds = 500,
                    WasapiLatencyMilliseconds = 50
                },
                LatencyMode.High => new LatencyConfiguration
                {
                    Mode = LatencyMode.High,
                    BufferMilliseconds = 1000,
                    WasapiLatencyMilliseconds = 100
                },
                _ => GetConfiguration(LatencyMode.Low)
            };
        }

        public override string ToString()
        {
            return Mode switch
            {
                LatencyMode.UltraLow => "Ultra Low (~10-20ms) - Movies/Gaming",
                LatencyMode.Low => "Low (~30-50ms) - Balanced",
                LatencyMode.Normal => "Normal (~50-100ms) - Stable",
                LatencyMode.High => "High (~100-200ms) - Maximum Stability",
                _ => "Unknown"
            };
        }
    }
}
