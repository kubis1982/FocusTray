# FocusTray Project Guidelines

A minimalist Windows system tray application that automates Deep Work sessions by synchronizing task timers with Microsoft Teams presence status.

## Architecture

**Clean Architecture (3-layer separation):**
- **FocusTray** (net10.0-windows): WPF UI + Application orchestration
- **FocusTray.Core** (net10.0): Domain models, business logic, service interfaces
- **FocusTray.Infrastructure** (net10.0): External integrations (Teams API, Auth, Notifications)

**Key principle**: Interfaces defined in Core → Implementations in Infrastructure. Follow dependency injection patterns throughout.

## Technology Stack

- **.NET 10.0** with self-contained deployment (`PublishSingleFile=true`)
- **WPF** for Windows desktop UI
- **Microsoft.Graph 5.103.0** - Teams presence API
- **Microsoft.Identity.Client 4.83.1** - OAuth2 via MSAL.NET
- **H.NotifyIcon.Wpf 2.4.1** - System tray management
- **Serilog** - Structured logging with file sinks
- **xUnit v3** with **AwesomeAssertions** for testing

## Code Style

```csharp
// ✅ Follow these patterns
public async Task<bool> StartFocusSessionAsync(string taskDescription, int durationMinutes)
{
    _logger.Information("Starting focus session: {Task} for {Duration} minutes", 
        taskDescription, durationMinutes);
    return await _teamsService.SetDoNotDisturbAsync(taskDescription);
}

// Use structured logging (not string interpolation)
// File-scoped namespaces: namespace FocusTray;
// Nullable reference types enabled
// Private fields: _camelCase
// Async methods: MethodAsync suffix
```

## Build and Test

**Build**: `dotnet build FocusTray.slnx`  
**Test**: `dotnet test`  
**Publish**: `dotnet publish src/FocusTray/FocusTray.csproj -c Release`

**Test naming**: BDD style with `Should_ExpectedBehavior_When_Condition`
- Example: `Should_ReturnTrue_When_StartingSessionWithValidDuration`
- Alternative: `Should_SetDoNotDisturbStatus_When_FocusSessionStarts`

## Key Conventions

1. **Graph API Pattern**: Use MSAL for auth → GraphServiceClient for Teams updates
2. **Error Handling**: Catch specific exceptions, log with context, return bool success indicators
3. **Testing**: Mock external dependencies, use real Graph SDK for integration tests
4. **Project Status**: Well-architected but feature-incomplete - core services need implementation