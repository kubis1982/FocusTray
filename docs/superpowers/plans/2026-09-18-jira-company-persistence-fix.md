# JIRA Company Persistence Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After a user logs in to JIRA, the entered company name must survive an app restart, so `TryAutoLoginAsync` can actually auto-login instead of always requiring a fresh manual login.

**Architecture:** `JiraAuthService.LoginAsync` (Infrastructure layer) already mutates the shared `JiraConfiguration` singleton's `Company` property in memory, and already persists the email/API token via `ICredentialService`. It never persists `Company` to `settings.json`, because that persistence (`SettingsService`, which lives in the UI/composition-root project `FocusTray`) cannot be referenced from the `Infrastructure` project without breaking the inward-pointing dependency rule (`FocusTray` → `FocusTray.Infrastructure` → `FocusTray.Core`). The fix stays in the UI layer instead: extract an `ISettingsService` interface (so it can be mocked in tests), inject it into `JiraLoginDialogViewModel` alongside the already-registered `JiraConfiguration` singleton, and call `SaveJiraConfiguration` once `IJiraAuthService.LoginAsync` reports success.

**Tech Stack:** .NET 10 / WPF, CommunityToolkit.Mvvm (`ObservableObject`, `[RelayCommand]`), Microsoft.Extensions.DependencyInjection, xUnit v3 + Moq + AwesomeAssertions.

**Spec:** No separate spec document — the requirement and root cause were established via code investigation in this conversation (see summary below). This plan doubles as the spec record.

### Confirmed root cause (do not re-investigate)

- `src/FocusTray.Infrastructure/Jira/JiraAuthService.cs:96` — `LoginAsync` does `_configuration.Company = company;` and never persists it.
- `src/FocusTray/ViewModels/JiraLoginDialogViewModel.cs:33-85` — the login command calls only `_authService.LoginAsync(...)`; it has no `SettingsService`/`ISettingsService` dependency at all.
- `src/FocusTray/Views/JiraAdvancedSettingsDialog.xaml.cs:77-110` — the only other place that calls `SettingsService.SaveJiraConfiguration`, and it only ever touches `JqlFilter`, never `Company`.
- `src/FocusTray.Infrastructure/Jira/JiraAuthService.cs:195-242` — `TryAutoLoginAsync` bails out at line 217 (`if (string.IsNullOrWhiteSpace(_configuration.Company))`) on every fresh process start, because `_configuration.Company` is reloaded from `settings.json` at DI-singleton-creation time (`App.xaml.cs:42-46`) and `settings.json` never received the `Company` value.
- Credentials (email + API token) **are** persisted correctly via `WindowsCredentialService` (DPAPI file under `%LocalAppData%\FocusTray\credentials\`), so only the `Company` field is affected.

## Global Constraints

- Dependencies must keep pointing inward: `FocusTray` → `FocusTray.Infrastructure` → `FocusTray.Core`. Do not add a reference from `FocusTray.Infrastructure` to `FocusTray` (the UI project) to "fix this from `JiraAuthService`" — the fix belongs in the UI layer (`JiraLoginDialogViewModel`).
- Async methods keep the `Async` suffix; service methods return `bool`/log rather than throw, per `CLAUDE.md`.
- Test naming is BDD-style: `Should_ExpectedBehavior_When_Condition`.
- Unit tests live in `tests/FocusTray.Tests` and mock all external-facing interfaces with Moq + AwesomeAssertions (`.Should()`), following the exact style already used in `tests/FocusTray.Tests/ViewModels/SessionConfigDialogViewModelTests.cs` and `tests/FocusTray.Tests/Infrastructure/JiraServiceTests.cs`.
- File-scoped namespaces, nullable reference types enabled (already the project default via `Directory.Build.props`; new files must follow it).
- Follow the existing codebase convention of adding a one-line `/// <summary>` XML doc comment on new public types/members (every existing service/interface in this codebase has one).

---

### Task 1: Persist JIRA company after a successful login

**Files:**
- Create: `src/FocusTray/Services/ISettingsService.cs`
- Modify: `src/FocusTray/Services/SettingsService.cs` (implement the new interface — one-line class declaration change, no logic change)
- Modify: `src/FocusTray/App.xaml.cs:33` (register the interface against the existing singleton instance)
- Modify: `src/FocusTray/ViewModels/JiraLoginDialogViewModel.cs` (constructor + `LoginAsync` command)
- Test: `tests/FocusTray.Tests/ViewModels/JiraLoginDialogViewModelTests.cs` (new file)

**Interfaces:**
- Consumes: `FocusTray.Infrastructure.Jira.JiraConfiguration` (existing model, `Company` / `JqlFilter` string properties, already a DI singleton per `App.xaml.cs:42-46`); `FocusTray.Core.Services.IJiraAuthService.LoginAsync(string company, string email, string apiToken) : Task<bool>` (existing, unchanged).
- Produces: `FocusTray.Services.ISettingsService` with `JiraConfiguration JiraConfiguration { get; }` and `void SaveJiraConfiguration(JiraConfiguration jiraConfig)` — later tasks/consumers (e.g. any future dialog) should depend on this interface, not the concrete `SettingsService`, when they need to be unit-testable.

- [ ] **Step 1: Write the failing tests**

Create `tests/FocusTray.Tests/ViewModels/JiraLoginDialogViewModelTests.cs`:

```csharp
using AwesomeAssertions;
using FocusTray.Core.Services;
using FocusTray.Infrastructure.Jira;
using FocusTray.Services;
using FocusTray.ViewModels;
using Moq;
using Xunit;

namespace FocusTray.Tests.ViewModels;

public class JiraLoginDialogViewModelTests
{
    private readonly Mock<IJiraAuthService> _mockAuthService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly JiraConfiguration _configuration;
    private readonly JiraLoginDialogViewModel _viewModel;

    public JiraLoginDialogViewModelTests()
    {
        _mockAuthService = new Mock<IJiraAuthService>();
        _mockSettingsService = new Mock<ISettingsService>();
        _configuration = new JiraConfiguration();

        _viewModel = new JiraLoginDialogViewModel(
            _mockAuthService.Object,
            _mockSettingsService.Object,
            _configuration);
    }

    [Fact]
    public async Task Should_SaveJiraConfiguration_When_LoginSucceeds()
    {
        // Arrange
        _viewModel.Company = "acme";
        _viewModel.Email = "user@example.com";
        _viewModel.ApiToken = "token123";

        _mockAuthService
            .Setup(x => x.LoginAsync("acme", "user@example.com", "token123"))
            .Callback(() => _configuration.Company = "acme")
            .ReturnsAsync(true);
        _mockAuthService.Setup(x => x.CurrentUsername).Returns("Test User");

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _viewModel.IsSuccess.Should().BeTrue();
        _mockSettingsService.Verify(x => x.SaveJiraConfiguration(_configuration), Times.Once);
    }

    [Fact]
    public async Task Should_NotSaveJiraConfiguration_When_LoginFails()
    {
        // Arrange
        _viewModel.Company = "acme";
        _viewModel.Email = "user@example.com";
        _viewModel.ApiToken = "wrong-token";

        _mockAuthService
            .Setup(x => x.LoginAsync("acme", "user@example.com", "wrong-token"))
            .ReturnsAsync(false);

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _viewModel.IsSuccess.Should().BeFalse();
        _mockSettingsService.Verify(x => x.SaveJiraConfiguration(It.IsAny<JiraConfiguration>()), Times.Never);
    }

    [Fact]
    public async Task Should_NotAttemptLogin_When_CompanyIsEmpty()
    {
        // Arrange
        _viewModel.Company = "";
        _viewModel.Email = "user@example.com";
        _viewModel.ApiToken = "token123";

        // Act
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // Assert
        _mockAuthService.Verify(
            x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        _mockSettingsService.Verify(x => x.SaveJiraConfiguration(It.IsAny<JiraConfiguration>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/FocusTray.Tests/FocusTray.Tests.csproj --filter "FullyQualifiedName~JiraLoginDialogViewModelTests"`

Expected: build error — `FocusTray.Services.ISettingsService` does not exist yet, and `JiraLoginDialogViewModel` has no 3-argument constructor.

- [ ] **Step 3: Create the `ISettingsService` interface**

Create `src/FocusTray/Services/ISettingsService.cs`:

```csharp
using FocusTray.Infrastructure.Jira;

namespace FocusTray.Services;

/// <summary>
/// Provides access to persisted, non-secret application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current JIRA configuration.
    /// </summary>
    JiraConfiguration JiraConfiguration { get; }

    /// <summary>
    /// Saves the JIRA configuration.
    /// </summary>
    void SaveJiraConfiguration(JiraConfiguration jiraConfig);
}
```

- [ ] **Step 4: Make `SettingsService` implement the interface**

Modify `src/FocusTray/Services/SettingsService.cs:11`, change:

```csharp
public class SettingsService
```

to:

```csharp
public class SettingsService : ISettingsService
```

No other change in this file — `JiraConfiguration` and `SaveJiraConfiguration` already match the interface signature exactly.

- [ ] **Step 5: Register the interface in DI**

Modify `src/FocusTray/App.xaml.cs`, right after line 33 (`services.AddSingleton<SettingsService>();`), add:

```csharp
services.AddSingleton<ISettingsService>(provider => provider.GetRequiredService<SettingsService>());
```

This keeps a single shared `SettingsService` instance reachable both by its concrete type (used by `JiraAdvancedSettingsDialog`) and by the new interface (used by `JiraLoginDialogViewModel`).

- [ ] **Step 6: Update `JiraLoginDialogViewModel` to persist `Company` on success**

Modify `src/FocusTray/ViewModels/JiraLoginDialogViewModel.cs`. Replace the top of the file:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusTray.Core.Services;

namespace FocusTray.ViewModels;

/// <summary>
/// ViewModel for JiraLoginDialog.
/// </summary>
public partial class JiraLoginDialogViewModel(IJiraAuthService authService) : ObservableObject
{
    private readonly IJiraAuthService _authService = authService ?? throw new ArgumentNullException(nameof(authService));
```

with:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusTray.Core.Services;
using FocusTray.Infrastructure.Jira;
using FocusTray.Services;

namespace FocusTray.ViewModels;

/// <summary>
/// ViewModel for JiraLoginDialog.
/// </summary>
public partial class JiraLoginDialogViewModel(
    IJiraAuthService authService,
    ISettingsService settingsService,
    JiraConfiguration configuration) : ObservableObject
{
    private readonly IJiraAuthService _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    private readonly ISettingsService _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    private readonly JiraConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
```

Then, inside the `LoginAsync` command method, change:

```csharp
            if (success)
            {
                StatusMessage = $"✓ Successfully logged in as {_authService.CurrentUsername}!";
                IsSuccess = true;
            }
```

to:

```csharp
            if (success)
            {
                _settingsService.SaveJiraConfiguration(_configuration);

                StatusMessage = $"✓ Successfully logged in as {_authService.CurrentUsername}!";
                IsSuccess = true;
            }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/FocusTray.Tests/FocusTray.Tests.csproj --filter "FullyQualifiedName~JiraLoginDialogViewModelTests"`

Expected: 3 passed, 0 failed.

- [ ] **Step 8: Commit**

```bash
git add src/FocusTray/Services/ISettingsService.cs src/FocusTray/Services/SettingsService.cs src/FocusTray/App.xaml.cs src/FocusTray/ViewModels/JiraLoginDialogViewModel.cs tests/FocusTray.Tests/ViewModels/JiraLoginDialogViewModelTests.cs
git commit -m "fix: persist JIRA company after successful login so auto-login survives restart"
```

---

### Task 2: Full regression check

**Files:** none (verification only).

- [ ] **Step 1: Run the full unit test suite**

Run: `dotnet test tests/FocusTray.Tests/FocusTray.Tests.csproj`

Expected: all tests pass, including the 3 new ones from Task 1 and every pre-existing test (`JiraServiceTests`, `SessionConfigDialogViewModelTests`, `TimerServiceTests`, `TimeOnlyStringConverterTests`). If anything else regresses, stop and investigate before continuing — do not proceed to Task 3 with a red suite.

- [ ] **Step 2: Build the whole solution**

Run: `dotnet build FocusTray.slnx`

Expected: build succeeds with no new warnings/errors introduced by this change.

---

### Task 3: Manual end-to-end verification

**Files:** none (manual verification only — this exercises the actual bug reported by the user).

- [ ] **Step 1: Clear any pre-existing state**

Close the app if running, then delete `%LocalAppData%\FocusTray\settings.json` (back it up first if it has real data you care about).

- [ ] **Step 2: Run the app and log in to JIRA**

Run: `dotnet run --project src/FocusTray/FocusTray.csproj`

In the tray icon menu, open the JIRA login dialog and log in with a real (or test) JIRA company, email, and API token. Confirm the dialog shows "✓ Successfully logged in as ..." and closes.

- [ ] **Step 3: Confirm `Company` was persisted**

Open `%LocalAppData%\FocusTray\settings.json` in a text editor and confirm the `Jira.Company` field now contains the company you entered (previously it stayed empty/default here).

- [ ] **Step 4: Restart and confirm auto-login**

Close the app fully and run it again (`dotnet run --project src/FocusTray/FocusTray.csproj`). Check `logs/focustray.log` (rolling file under the working directory) for an `"Auto-login successful"` entry from `JiraAuthService.TryAutoLoginAsync`, and confirm the tray/session UI shows JIRA as connected without prompting for login again.

- [ ] **Step 5: Report result**

If step 4 shows `"Company not configured, cannot auto-login"` or login is still required every run, the fix is incomplete — return to Task 1 and re-investigate rather than declaring success.
