# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build FocusTray.slnx

# Run the app
dotnet run --project src/FocusTray/FocusTray.csproj

# Run all tests
dotnet test FocusTray.slnx

# Run a single test project
dotnet test tests/FocusTray.Tests/FocusTray.Tests.csproj
dotnet test tests/FocusTray.IntegrationTests/FocusTray.IntegrationTests.csproj

# Run a single test (xUnit v3 filter)
dotnet test tests/FocusTray.Tests/FocusTray.Tests.csproj --filter "FullyQualifiedName~TimerServiceTests.Should_StartSession_When_ValidDurationProvided"

# Publish self-contained release build
dotnet publish src/FocusTray/FocusTray.csproj -c Release

# MSIX packaging (Windows only)
.\build\scripts\Create-SelfSignedCert.ps1
.\build\scripts\Build-MSIX.ps1
```

JIRA integration tests hit real Atlassian Cloud and are **skipped automatically** unless `JIRA_COMPANY`, `JIRA_EMAIL`, and `JIRA_API_TOKEN` env vars are set (see `tests/FocusTray.IntegrationTests/README.md`). Never hardcode credentials in test files.

Target framework is `net10.0` / `net10.0-windows10.0.19041.0` (WPF requires Windows to build/run the UI project). The solution uses the `.slnx` format.

## Architecture

Three-layer Clean Architecture, dependencies point inward (`FocusTray` → `FocusTray.Infrastructure` → `FocusTray.Core`, with `FocusTray` also referencing `Core` directly):

- **`FocusTray.Core`** (`net10.0`) — domain layer, no external dependencies beyond `Microsoft.Extensions.Logging.Abstractions`. Defines models (`FocusSession`, `JiraIssue`, `JiraWorklog`, `TeamsPresenceStatus`, `TimerState`) and service *interfaces* (`ITimerService`, `IJiraService`, `IJiraAuthService`, `ITeamsAuthService`, `ITeamsPresenceService`, `ICredentialService`). Only `TimerService` is implemented here (pure timer logic); every other interface is implemented in `Infrastructure`.
- **`FocusTray.Infrastructure`** (`net10.0`) — implements the Core interfaces against external systems:
  - `Jira/JiraService.cs` — talks to Atlassian Cloud via `Kubis1982.Atlassian.Jira.RestClient.v2` (a Kiota-generated typed client, e.g. `jiraClient.Rest.Api.Two.Myself.GetAsync()`), not raw `HttpClient` calls. Auth is Basic (email + API token) via `JiraAuthService`.
  - `Teams/TeamsAuthService.cs` — OAuth2 login to Microsoft Entra ID via MSAL (`Microsoft.Identity.Client`), interactive browser flow with silent-refresh/auto-login, persistent token cache in `MsalTokenCacheHelper`.
  - `Teams/TeamsPresenceService.cs` — uses `Microsoft.Graph` (`GraphServiceClient`) with a custom `IAccessTokenProvider` fed by `TeamsAuthService.GetAccessTokenAsync()` to set Teams presence to DoNotDisturb + a status message with expiry when a focus session starts, and clear it on session end.
  - `Credentials/WindowsCredentialService.cs` — stores JIRA/Teams secrets in Windows Credential Manager (never in `settings.json`).
- **`FocusTray`** (`net10.0-windows10.0.19041.0`, WPF `WinExe`) — UI + composition root.
  - `App.xaml.cs` wires up the entire DI container by hand (`ServiceCollection` in `OnStartup`) — this is the single place all services, dialogs, and view models get registered. New services/dialogs must be registered here to be resolvable.
  - After building the container, `App` fires a non-blocking `TryAutoLoginAsync()` that attempts silent JIRA and Teams auto-login without surfacing errors to the user.
  - `MainWindow` hosts the system tray icon (H.NotifyIcon.Wpf) and its state (bell vs. bell-with-slash) mirrors `TimerState`.
  - `Views/` holds code-behind dialogs (JIRA login/settings, Teams login/settings, About, Session config/status); `ViewModels/` holds the MVVM view models (CommunityToolkit.Mvvm `ObservableObject`/`RelayCommand`) for the dialogs that have one — plain dialogs are driven from code-behind.
  - `Services/SettingsService.cs` persists non-secret configuration (JIRA company + JQL filter, session defaults) to `%LocalApplicationData%\FocusTray\settings.json` via `JsonSerializer`.

Session lifecycle: `ITimerService` drives start/pause/resume/stop; on completion, if the session is linked to a JIRA issue, the user is prompted to log time via `IJiraService`, and if Teams is connected, presence/status is set on start and cleared on end via `ITeamsPresenceService`.

## Conventions

- File-scoped namespaces, nullable reference types enabled, `LangVersion=latest` (set centrally in `Directory.Build.props`).
- Structured Serilog logging with named properties (`_logger.LogInformation("Starting session {Task} for {Duration} minutes", ...)`), never string interpolation into log messages.
- Service methods generally return `bool` success indicators rather than throwing, catching specific exceptions (e.g. `HttpRequestException`, `ServiceException` from Graph, `MsalException`) and logging with context.
- Async methods use the `Async` suffix.
- Test naming is BDD-style: `Should_ExpectedBehavior_When_Condition` (e.g. `Should_StartSession_When_ValidDurationProvided`).
- Unit tests (`FocusTray.Tests`) mock external dependencies (`IJiraService`, `ITimerService`, etc.) with Moq + AwesomeAssertions; integration tests (`FocusTray.IntegrationTests`) exercise the real JIRA API and require credentials.

## Secrets and config

- `%LocalApplicationData%\FocusTray\settings.json` holds only non-secret config (JIRA company, JQL filter) — never commit or write credentials here.
- JIRA/Teams credentials and tokens live in Windows Credential Manager / MSAL's persistent cache, accessed through `ICredentialService` / `MsalTokenCacheHelper`.
- `nuget.config` restricts package sources to `nuget.org` only.
