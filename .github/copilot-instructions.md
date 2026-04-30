# FocusTray Project Guidelines

A minimalist Windows system tray application that helps maintain deep focus during work sessions with JIRA integration for automatic worklog tracking.

## Architecture

**Clean Architecture (3-layer separation):**
- **FocusTray** (net10.0-windows): WPF UI layer + application orchestration
- **FocusTray.Core** (net10.0): Domain models and business logic (service interfaces)
- **FocusTray.Infrastructure** (net10.0): External integrations (JIRA API, HTTP client setup)

**Key principle**: Define service interfaces in Core → Implement them in Infrastructure. All layers use dependency injection (Microsoft.Extensions.DependencyInjection).

## Technology Stack

- **.NET 10.0** with self-contained deployment (`PublishSingleFile=true`, `RuntimeIdentifier=win-x64`)
- **WPF** for Windows desktop UI with MVVM (CommunityToolkit.Mvvm 8.4.2)
- **H.NotifyIcon.Wpf 2.4.1** - System tray icon management
- **CommunityToolkit.WinUI.Notifications 7.1.2** - Native Windows toast notifications
- **Serilog** - Structured logging (file sink, no string interpolation)
- **System.Net.Http.Json** - REST API client for JIRA
- **xUnit v3** + **AwesomeAssertions** - Testing framework
- **Moq 4.20.72** - Mocking for unit tests

## Key Components

**Core Layer:**
- `ITimerService` / `TimerService` - Focus session timer logic with pause/resume/extend
- `IJiraService` - JIRA integration interface (JIRA-specific implementation in Infrastructure)
- Domain models: `FocusSession`, `JiraIssue`, `JiraWorklog`, `TimerState` enum

**UI Layer:**
- MVVM pattern with ObservableObject, RelayCommand
- `SettingsService` - Configuration persistence to `%LocalApplicationData%\FocusTray\settings.json`
- WPF dialogs: SessionConfigDialog, SessionStatusDialog (with analog clock visualization)

**Infrastructure Layer:**
- `JiraService` - REST API implementation using HttpClient + Basic Auth (email + API token)
- HTTP client configured with base address and auth headers

## Code Style

```csharp
// ✅ Follow these patterns
namespace FocusTray.Core.Services;

public class TimerService : ITimerService
{
    private readonly ILogger<TimerService> _logger;

    public bool StartSession(string taskDescription, TimeSpan duration)
    {
        // Structured logging with named properties
        _logger.LogInformation("Starting session {Task} for {Duration} minutes", 
            taskDescription, duration.TotalMinutes);
        // Return bool success indicator, not throw
        return true;
    }
}

// Use file-scoped namespaces
// Enable nullable reference types
// Private fields: _camelCase
// Async methods: MethodAsync suffix
// Structured logging, not string interpolation
```

## Build and Test

**Build**: `dotnet build FocusTray.slnx`

**Run application**: `dotnet run --project src/FocusTray/FocusTray.csproj`

**Test all**: `dotnet test FocusTray.slnx`  
**Unit tests only**: `dotnet test tests/FocusTray.Tests/FocusTray.Tests.csproj`  
**Integration tests** (requires JIRA credentials): `dotnet test tests/FocusTray.IntegrationTests/FocusTray.IntegrationTests.csproj`

**Publish release** (self-contained .exe): `dotnet publish src/FocusTray/FocusTray.csproj -c Release`

**Test naming convention**: BDD style `Should_ExpectedBehavior_When_Condition`
- Good: `Should_StartSession_When_ValidDurationProvided`
- Good: `Should_ReturnFalse_When_SessionAlreadyRunning`

## JIRA Integration Details

- **Configuration file**: `%LocalApplicationData%\FocusTray\settings.json` (never commit)
- **Authentication**: Credentials stored securely in Windows Credential Manager; only company name and JQL filter in settings file
- **API endpoint pattern**: Assumes Atlassian Cloud (`https://{company}.atlassian.net/rest/api/3/...`)
- **Worklog on session end**: Optional user confirmation to log time to JIRA issue
- **Integration tests**: Skipped automatically if `JIRA_COMPANY`, `JIRA_EMAIL`, `JIRA_API_TOKEN` environment variables are not set

## Common Patterns

1. **Error Handling**: Catch specific exceptions (HttpRequestException, JsonException), log with context, return `bool` success indicators (not exceptions)
2. **Testing**: Mock external dependencies (IJiraService, ITimerService) in unit tests; use real HTTP for integration tests with credentials
3. **JIRA API calls**: Use `HttpClient.PostAsJsonAsync`, `GetAsync` with proper error handling and logging
4. **UI updates**: Use MVVM RelayCommand and data binding; avoid code-behind logic
5. **Settings persistence**: Serialize/deserialize via `JsonSerializer.Serialize` / `Deserialize` in SettingsService