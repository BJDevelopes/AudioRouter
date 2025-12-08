# Audio Router

A Windows desktop application that allows you to route and duplicate audio from any application to any output device without changing Windows settings or application configurations.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

## Features

- **Zero Configuration**: Works without modifying Windows audio settings or application settings
- **Application-Level Routing**: Select any running application and route its audio to any output device
- **Multiple Routes**: Create multiple audio routes simultaneously
- **Volume Control**: Adjust volume independently for each route
- **Real-Time Management**: Routes are temporary and cleared when the application closes
- **Auto-Discovery**: Automatically detects running applications with active audio sessions
- **Modern UI**: Clean, intuitive interface similar to Windows Volume Mixer

## How It Works

Audio Router uses **WASAPI (Windows Audio Session API) Loopback Capture** to:
1. Capture audio output from the system's default audio device
2. Buffer and process the audio stream
3. Forward it to your selected output device
4. Maintain independent volume control per route

**Important**: The application captures from the system's default playback device, so audio must be playing through your default device for routing to work.

## Requirements

- Windows 10 or Windows 11
- .NET 8.0 Runtime or SDK
- Administrative privileges may be required for some audio devices

## Installation

### Option 1: Build from Source

1. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

2. Clone the repository:
```bash
git clone <repository-url>
cd AudioRouter
```

3. Build the project:
```bash
dotnet build AudioRouter.sln --configuration Release
```

4. Run the application:
```bash
dotnet run --project AudioRouter/AudioRouter.csproj
```

### Option 2: Publish Standalone Executable

Create a standalone executable that doesn't require .NET runtime:

```bash
dotnet publish AudioRouter/AudioRouter.csproj -c Release -r win-x64 --self-contained
```

The executable will be in `AudioRouter/bin/Release/net8.0-windows/win-x64/publish/`

## Usage

### Creating an Audio Route

1. **Launch the Application**: Run AudioRouter.exe

2. **Select Source Application**:
   - The left panel shows all applications currently playing audio
   - Click on the application you want to route
   - Use "Refresh Applications" if your app isn't listed

3. **Select Output Device**:
   - Choose the target output device from the dropdown
   - This is where the audio will be duplicated to

4. **Start Route**:
   - Click "▶️ Start Audio Route"
   - The route appears in the "Active Routes" panel on the right

5. **Adjust Volume** (Optional):
   - Use the slider next to each active route to control its volume
   - Volume is independent from system volume

### Managing Routes

- **View Active Routes**: All active routes are displayed in the right panel
- **Adjust Volume**: Use the slider (🔊) for each route
- **Stop a Route**: Select a route and click "⏹️ Stop Selected Route"
- **Refresh Lists**: Click the refresh buttons to update applications or devices

### Tips

- Routes are automatically stopped if:
  - The source application is closed
  - The target device is disconnected
  - An error occurs during routing

- All routes are cleared when you close Audio Router

- For best results, ensure your source application is actively playing audio when creating a route

## Architecture

### Core Components

```
AudioRouter/
├── Models/
│   ├── AudioSession.cs        # Represents an audio session (application)
│   ├── AudioDeviceInfo.cs     # Represents an audio output device
│   └── AudioRoute.cs           # Represents an active audio route
├── Services/
│   ├── AudioSessionManager.cs     # Discovers active audio sessions
│   ├── AudioDeviceEnumerator.cs   # Lists available output devices
│   ├── AudioRouterEngine.cs       # Captures and routes audio
│   └── RoutingManager.cs          # Manages multiple routes
├── Converters/
│   └── VolumeConverter.cs      # Converts volume between UI and model
└── MainWindow.xaml/cs          # Main UI
```

### Technology Stack

- **WPF (Windows Presentation Foundation)**: Modern UI framework
- **NAudio**: Audio processing library
  - WASAPI Loopback Capture: Captures audio from applications
  - WASAPI Out: Outputs audio to devices
  - Volume Sample Provider: Per-route volume control
- **.NET 8.0**: Latest .NET framework

## Limitations

- Only captures audio from the **default playback device**
- Cannot route audio from applications using exclusive mode
- Requires applications to be actively playing audio
- Small latency (~50ms) may be noticeable in some scenarios
- Does not support per-application capture (captures all system audio on default device)

## Troubleshooting

### Application Not Showing in List
- Ensure the application is actively playing audio
- Click "Refresh Applications"
- Verify the application is using the default audio device

### No Audio on Target Device
- Check that the source application is playing audio
- Verify the route shows 🟢 (active)
- Adjust the route's volume slider
- Ensure the target device is set as a playback device (not disabled)

### Poor Audio Quality or Crackling
- Reduce buffer size (requires code modification)
- Close other audio-intensive applications
- Update your audio drivers

### Route Automatically Stops
- The source application may have stopped playing audio
- The target device may have been disconnected
- Check the status bar for error messages

## Advanced Configuration

### Changing Buffer Duration

Edit `AudioRouterEngine.cs` line 67:

```csharp
BufferDuration = TimeSpan.FromSeconds(2), // Increase for stability, decrease for lower latency
```

### Changing WASAPI Latency

Edit `AudioRouterEngine.cs` line 79:

```csharp
new WasapiOut(outputDevice, AudioClientShareMode.Shared, false, 50); // 50ms latency
```

## Development

### Project Structure

- Built with .NET 8.0 and C# 12
- Uses MVVM pattern for UI data binding
- Asynchronous audio processing with Task-based API
- Event-driven architecture for route management

### Adding Features

To add new features, consider these extension points:
- Custom audio effects: Insert between `BufferedWaveProvider` and `VolumeSampleProvider`
- Per-application capture: Implement process-specific WASAPI capture
- Audio visualization: Subscribe to the `DataAvailable` event
- Presets/Profiles: Serialize `AudioRoute` objects

## Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- [NAudio](https://github.com/naudio/NAudio) - Excellent audio library for .NET
- Microsoft WASAPI documentation
- Windows Audio Session API (WASAPI)

## Disclaimer

This software is provided as-is without any warranty. Use at your own risk. The authors are not responsible for any audio issues, system crashes, or other problems that may arise from using this software.
