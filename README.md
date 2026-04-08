# FocusTray

A minimalist Windows system tray application designed to help you maintain deep focus during work sessions by providing visual feedback and non-intrusive notifications.

![FocusTray Icon](src/FocusTray/Resources/bell-icon.ico)

## Overview

FocusTray is a lightweight productivity tool that lives in your Windows system tray, helping you stay focused on your work with configurable focus sessions. The application provides clear visual feedback through dynamic tray icons and elegant toast notifications positioned above your taskbar.

## Key Features

- **🔔 Smart System Tray Integration**: Professional bell icons that change color based on session state
  - White bell icon when idle
  - Green bell icon during active focus sessions
  
- **⏱️ Customizable Focus Sessions**: Define your own work intervals
  - Configure task descriptions for each session
  - Set custom durations (from 1 minute to 24 hours)
  - Default 25-minute Pomodoro-style sessions

- **📢 Non-Intrusive Notifications**: Elegant toast notifications that don't interrupt your flow
  - Positioned above the Windows taskbar for visibility
  - Auto-dismiss after 5 seconds
  - Smooth fade-in and fade-out animations

- **🎯 Visual Session Tracking**: Real-time session monitoring
  - Live countdown in system tray tooltip
  - Analog clock visualization in session status dialog
  - Session progress indicator

- **🔄 Session Management**: Full control over your focus time
  - Start, pause, resume, and stop sessions
  - Manual session termination when needed
  - Session state persistence

## Installation

### Prerequisites

- Windows 10 or later
- .NET 10.0 Runtime (included with self-contained builds)

### Build from Source

```bash
# Clone the repository
git clone https://github.com/kubis1982/FocusTray.git
cd FocusTray

# Build the solution
dotnet build FocusTray.slnx

# Run the application
dotnet run --project src/FocusTray/FocusTray.csproj

# Create a release build
dotnet publish src/FocusTray/FocusTray.csproj -c Release
```

## Usage

### Starting a Focus Session

1. **Right-click** the FocusTray icon in your system tray
2. Select **"Start Focus Session"**
3. Enter a task description (e.g., "Code review", "Write documentation")
4. Set the duration in minutes (default: 25)
5. Click **"Start"**

The tray icon will turn green, indicating an active focus session.

### Monitoring Your Session

- **Hover** over the tray icon to see remaining time
- **Right-click** and select **"View Current Session"** to see detailed progress
- The tooltip updates every second with remaining time

### Ending a Session

Sessions automatically complete when the timer expires, showing a completion notification.

To manually end a session:
1. **Right-click** the tray icon
2. Select **"End Session"**
3. Confirm the action

### Exiting the Application

1. **Right-click** the tray icon
2. Select **"Exit"**

## Technical Architecture

FocusTray follows **Clean Architecture** principles with clear separation of concerns:

```
FocusTray/
├── src/
│   ├── FocusTray/              # WPF UI Layer (net10.0-windows)
│   │   ├── MainWindow.xaml     # System tray integration
│   │   ├── SessionConfigDialog.xaml    # Session configuration
│   │   └── SessionStatusDialog.xaml    # Session monitoring
│   ├── FocusTray.Core/         # Domain Layer (net10.0)
│   │   ├── Models/             # Domain entities (FocusSession, TimerState)
│   │   └── Services/           # Business logic (ITimerService, TimerService)
│   └── FocusTray.Infrastructure/   # Infrastructure Layer (net10.0)
│       └── (Reserved for future integrations)
└── tests/
    ├── FocusTray.Tests/        # Unit tests
    └── FocusTray.IntegrationTests/  # Integration tests
```

### Key Components

**Core Layer** (`FocusTray.Core`)
- `ITimerService`: Interface for timer management
- `TimerService`: Implementation of focus session timer logic
- `FocusSession`: Domain model representing a work session
- `TimerState`: Enum for session states (Idle, Running, Paused, Completed)

**UI Layer** (`FocusTray`)
- WPF application with system tray integration
- Uses H.NotifyIcon.Wpf for taskbar icon management
- WPF-UI (Wpf.Ui) for modern notification design
- Custom analog clock visualization

**Infrastructure Layer** (`FocusTray.Infrastructure`)
- Extensible for future integrations (notifications, analytics, etc.)
- Currently minimal to keep the application lightweight

## Technology Stack

- **.NET 10.0** - Latest .NET platform
- **WPF** - Windows Presentation Foundation for desktop UI
- **H.NotifyIcon.Wpf 2.4.1** - System tray icon management
- **WPF-UI 4.2.0** - Modern UI components and notifications
- **Serilog** - Structured logging
- **xUnit v3** - Unit testing framework
- **AwesomeAssertions** - Fluent assertion library

## Development

### Running Tests

```bash
# Run all tests
dotnet test FocusTray.slnx

# Run tests with coverage
dotnet test FocusTray.slnx --collect:"XPlat Code Coverage"
```

### Project Structure

The solution uses `.slnx` format for better performance and modern tooling support.

### Dependencies

All NuGet dependencies are managed centrally through the solution file. Key packages:

- **Microsoft.Extensions.DependencyInjection** - Dependency injection
- **Serilog.Sinks.File** - File-based logging
- **H.NotifyIcon.Wpf** - System tray functionality
- **WPF-UI** - Modern UI components

## Configuration

FocusTray requires minimal configuration:

- **Default Session Duration**: 25 minutes (Pomodoro technique)
- **Maximum Duration**: 24 hours (1440 minutes)
- **Notification Duration**: 5 seconds
- **Update Interval**: 1 second

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Icon design inspired by productivity and focus themes
- Built with modern .NET 10 and WPF technologies

## Support

For issues, questions, or suggestions:
- Open an issue on [GitHub Issues](../../issues)
- Check existing documentation and FAQs

---

**Made with ❤️ for focused productivity**
