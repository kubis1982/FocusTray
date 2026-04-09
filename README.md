# FocusTray

[![GitHub release (latest by date)](https://img.shields.io/github/v/release/kubis1982/FocusTray)](https://github.com/kubis1982/FocusTray/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![WinGet](https://img.shields.io/badge/WinGet-Kubis1982.FocusTray-blue)](https://github.com/microsoft/winget-pkgs)

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
  - **🎯 JIRA Integration**: Link sessions to JIRA issues with automatic worklog tracking
  - Set custom durations (from 1 minute to 24 hours)
  - Default 25-minute Pomodoro-style sessions

- **📋 JIRA Worklog Automation**: Seamless time tracking for Atlassian Cloud
  - Select JIRA issues directly from your assigned tasks
  - Automatic worklog entries when sessions complete
  - Configurable JQL filters for issue selection
  - Optional - works standalone without JIRA

- **📢 Non-Intrusive Notifications**: Native Windows toast notifications
  - True system notifications using Windows Action Center
  - Auto-dismiss after configured time
  - Clean, native Windows 10/11 appearance

- **🎯 Visual Session Tracking**: Real-time session monitoring
  - Live countdown in system tray tooltip
  - Analog clock visualization in session status dialog
  - Session progress indicator

- **🔄 Session Management**: Full control over your focus time
  - Start, pause, resume, and stop sessions
  - Manual session termination when needed
  - Session state persistence

## Installation

### Via WinGet (Recommended)

```bash
winget install Kubis1982.FocusTray
```

### Via GitHub Releases

Download the latest release from [GitHub Releases](https://github.com/kubis1982/FocusTray/releases) and run the executable.

### Prerequisites

- Windows 10 (build 19041) or later
- No additional dependencies required (.NET 10.0 Runtime is embedded)

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

### JIRA Integration Setup (Optional)

FocusTray can automatically track time in JIRA Atlassian Cloud:

1. **Generate a JIRA API Token**:
   - Visit https://id.atlassian.com/manage-profile/security/api-tokens
   - Click "Create API token"
   - Give it a name (e.g., "FocusTray")
   - Copy the generated token

2. **Configure JIRA in FocusTray**:
   - Right-click the tray icon
   - Select **"JIRA Settings"**
   - Enter your JIRA details:
     - Base URL: `https://yourcompany.atlassian.net`
     - Email: Your Atlassian account email
     - API Token: Paste the token you generated
   - (Optional) Customize JQL filter for issue selection
   - Click **"Test Connection"** to verify
   - Click **"Save"**

3. **Default JQL Filter**: 
   ```
   assignee = currentUser() AND statusCategory != Done ORDER BY updated DESC
   ```
   This shows your assigned, incomplete issues sorted by most recently updated.

**⚠️ Security Note**: JIRA credentials are stored locally in:  
`%LocalApplicationData%\FocusTray\settings.json`  
Keep this file secure and never commit it to source control.

### Starting a Focus Session

1. **Right-click** the FocusTray icon in your system tray
2. Select **"Start Focus Session"**
3. Choose your work tracking method:
   - **Manual Description**: Enter a task description (e.g., "Code review")
   - **JIRA Issue** (if configured): Check "Use JIRA issue", select an issue from the dropdown
4. Set the duration in minutes (default: 25)
5. Click **"Start"**

The tray icon will turn green, indicating an active focus session.

### Completing a Session with JIRA

When a session linked to a JIRA issue completes:
1. A prompt will ask: **"Log 25 minutes to JIRA-123?"**
2. Click **"Yes"** to create a worklog entry in JIRA
3. Click **"No"** to skip worklog creation

The worklog entry includes:
- Time spent (from session duration)
- Session description as a comment
- Automatically timestamped to when the session started

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
│   │   ├── SessionConfigDialog.xaml    # Session configuration with JIRA
│   │   ├── SessionStatusDialog.xaml    # Session monitoring
│   │   ├── ViewModels/         # MVVM ViewModels
│   │   └── Services/           # Settings persistence
│   ├── FocusTray.Core/         # Domain Layer (net10.0)
│   │   ├── Models/             # Domain entities (FocusSession, JiraIssue, etc.)
│   │   └── Services/           # Business logic interfaces
│   └── FocusTray.Infrastructure/   # Infrastructure Layer (net10.0)
│       └── Jira/               # JIRA REST API integration
└── tests/
    ├── FocusTray.Tests/        # Unit tests (with mocks)
    └── FocusTray.IntegrationTests/  # Integration tests (optional, needs credentials)
```

### Key Components

**Core Layer** (`FocusTray.Core`)
- `ITimerService`: Interface for timer management
- `TimerService`: Implementation of focus session timer logic
- `IJiraService`: Interface for JIRA integration
- `FocusSession`: Domain model representing a work session
- `JiraIssue`: Domain model for JIRA issues
- `JiraWorklog`: Domain model for worklog entries
- `TimerState`: Enum for session states (Idle, Running, Paused, Completed)

**UI Layer** (`FocusTray`)
- WPF application with system tray integration
- MVVM pattern with CommunityToolkit.Mvvm
- SessionConfigDialogViewModel for JIRA issue selection
- SettingsService for configuration persistence
- Uses H.NotifyIcon.Wpf for taskbar icon management
- CommunityToolkit.WinUI.Notifications for native Windows toast notifications
- Custom analog clock visualization

**Infrastructure Layer** (`FocusTray.Infrastructure`)
- `JiraService`: REST API integration with Atlassian Cloud
- HTTP-based communication using System.Net.Http.Json
- Basic Authentication with email + API token
- Configuration model with validation

## Technology Stack

- **.NET 10.0** - Latest .NET platform with Windows 10.0.19041.0 target
- **WPF** - Windows Presentation Foundation for desktop UI
- **CommunityToolkit.Mvvm 8.4.2** - MVVM framework with source generators
- **H.NotifyIcon.Wpf 2.4.1** - System tray icon management
- **CommunityToolkit.WinUI.Notifications 7.1.2** - Native Windows toast notifications
- **Microsoft.Graph 5.103.0** - Microsoft Teams integration (future)
- **System.Net.Http.Json** - JSON serialization for REST APIs
- **Serilog** - Structured logging
- **xUnit v3** - Unit testing framework
- **AwesomeAssertions** - Fluent assertion library
- **Moq 4.20.72** - Mocking framework for tests

## Development

### Running Tests

```bash
# Run all tests
dotnet test FocusTray.slnx

# Run unit tests only (no credentials needed)
dotnet test tests/FocusTray.Tests/FocusTray.Tests.csproj

# Run integration tests (requires JIRA credentials)
# Set environment variables first:
# $env:JIRA_BASE_URL = "https://yourcompany.atlassian.net"
# $env:JIRA_EMAIL = "your.email@company.com"
# $env:JIRA_API_TOKEN = "your-api-token"
dotnet test tests/FocusTray.IntegrationTests/FocusTray.IntegrationTests.csproj

# Run tests with coverage
dotnet test FocusTray.slnx --collect:"XPlat Code Coverage"
```

**Note**: Integration tests are automatically **skipped** if JIRA environment variables are not set.
See `tests/FocusTray.IntegrationTests/README.md` for detailed setup instructions.

### Project Structure

The solution uses `.slnx` format for better performance and modern tooling support.

### Dependencies

All NuGet dependencies are managed centrally through the solution file. Key packages:

- **Microsoft.Extensions.DependencyInjection** - Dependency injection
- **Serilog.Sinks.File** - File-based logging
- **H.NotifyIcon.Wpf** - System tray functionality
- **CommunityToolkit.WinUI.Notifications** - Native Windows toast notifications

## Configuration

FocusTray stores configuration in:  
**`%LocalApplicationData%\FocusTray\settings.json`**

### Session Settings
- **Default Session Duration**: 25 minutes (Pomodoro technique)
- **Maximum Duration**: 24 hours (1440 minutes)
- **Notification Type**: Native Windows toast notifications
- **Update Interval**: 1 second

### JIRA Settings (Optional)
When JIRA integration is enabled, the settings file contains:
```json
{
  "JiraConfiguration": {
    "Enabled": true,
    "BaseUrl": "https://yourcompany.atlassian.net",
    "Email": "your.email@company.com",
    "ApiToken": "your-api-token-here",
    "JqlFilter": "assignee = currentUser() AND statusCategory != Done"
  }
}
```

**⚠️ Security**: 
- Never commit `settings.json` to source control
- Keep your API token secure
- Settings file is created on first configuration save

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
