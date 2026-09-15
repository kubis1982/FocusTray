# Migracja integracji JIRA na OAuth 2.0 (3LO) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace JIRA's Basic Auth (email + API token) with OAuth 2.0 Authorization Code + PKCE (Atlassian 3LO), mirroring the browser-based login UX already shipped for Microsoft Teams.

**Architecture:** `JiraAuthService` runs a local-loopback Authorization Code + PKCE flow (via the `IdentityModel` NuGet package, since Atlassian has no MSAL equivalent and is pure OAuth2, not OIDC), stores tokens in a new DPAPI-encrypted `JiraTokenCacheHelper` file cache, and resolves the Atlassian `cloudId` via the `accessible-resources` endpoint. `JiraService` keeps the existing Kiota-generated REST v2 client but builds it from a raw `IRequestAdapter` (bypassing `JiraRestClient.Create`, which hardcodes `https://{company}.atlassian.net`) so it can target `https://api.atlassian.com/ex/jira/{cloudId}/` with a Bearer auth provider.

**Tech Stack:** .NET 10 (net10.0 / net10.0-windows10.0.19041.0), WPF, CommunityToolkit.Mvvm, `Kubis1982.Atlassian.Jira.RestClient.v2` 1.8464.1 (Kiota-generated), Microsoft.Kiota.Abstractions 1.22.1, `IdentityModel` 7.0.0 (new), xUnit v3 + Moq + AwesomeAssertions.

**Spec:** `docs/superpowers/specs/2026-09-15-jira-oauth2-design.md`

## Global Constraints

- Target frameworks stay as-is: `FocusTray.Infrastructure` = `net10.0`; `FocusTray` and `FocusTray.Tests` = `net10.0-windows10.0.19041.0`.
- New dependency: `IdentityModel` version `7.0.0` exactly (verified installable from nuget.org; do not use `IdentityModel.OidcClient` — Atlassian 3LO is pure OAuth2, not OIDC, and that package assumes OIDC semantics like id_token validation).
- No `client_secret` anywhere in code or config — the Atlassian app is registered as a **public client with PKCE** (per approved spec). `AuthorizationCodeTokenRequest`/`RefreshTokenRequest` must never set `ClientSecret`.
- UI-facing strings (dialog text, status messages, tooltips) must be in **English**, matching the existing FocusTray convention (see `TeamsLoginDialog`/current `JiraLoginDialog`). Task prose in this plan is Polish; code and UI copy are not.
- `JiraRestClient.Create(string, IAuthenticationProvider, HttpClient)` must **not** be used for the OAuth-authenticated client — it internally builds `https://{firstArg}.atlassian.net`. Use `new JiraRestClient(IRequestAdapter)` (public constructor confirmed via reflection) with an adapter built by `Kubis1982.Atlassian.RestClient.HttpClientRequestAdapterFactory.Create(baseUrl, authProvider, httpClient)` instead.
- Full replacement, no Basic Auth fallback: `IJiraAuthService.LoginAsync()` becomes parameterless (breaking change, mirrors `ITeamsAuthService.LoginAsync()`).
- Redirect URI port: `http://localhost:8082/callback` (Teams already uses `8080`; pick a distinct port to avoid collisions if a user runs both flows back to back).

## Prerequisite (manual, human-only — cannot be automated by an agentic worker)

Before Task 7 can be exercised against real Atlassian servers, a human must register an OAuth 2.0 (3LO) app in the [Atlassian Developer Console](https://developer.atlassian.com/console/myapps):

1. Create app → **OAuth 2.0 (3LO)** integration type, **public client** (no client secret).
2. Add Jira Cloud API permissions/scopes: `read:jira-work`, `write:jira-work`, `read:jira-user`, `offline_access`.
3. Set **Callback URL** to `http://localhost:8082/callback`.
4. Copy the generated **Client ID**.

This Client ID replaces the placeholder value `"REPLACE_WITH_ATLASSIAN_OAUTH_CLIENT_ID"` written in Task 1's `JiraOAuthConfiguration.ClientId`. Until it's replaced, the code compiles and all unit tests (which don't call real Atlassian endpoints) pass, but `LoginAsync()` will fail against the real authorization server. This mirrors how `TeamsConfiguration.ClientId` already requires a prior, manual Azure AD App Registration — it is not something code can invent.

---

### Task 1: `JiraOAuthConfiguration` — static OAuth config + URL builders

**Files:**
- Create: `src/FocusTray.Infrastructure/Jira/JiraOAuthConfiguration.cs`
- Test: `tests/FocusTray.Tests/Infrastructure/JiraOAuthConfigurationTests.cs`

**Interfaces:**
- Produces: `JiraOAuthConfiguration.ClientId` (string), `.RedirectUri` (string), `.AuthorizationEndpoint` (string), `.TokenEndpoint` (string), `.AccessibleResourcesEndpoint` (string), `.Scopes` (string[]), `JiraOAuthConfiguration.BuildApiBaseUrl(string cloudId) : string`, `JiraOAuthConfiguration.BuildAuthorizationUrl(string codeChallenge, string state) : string` — used by Tasks 7 and 9.

- [ ] **Step 1: Write the failing tests**

```csharp
using AwesomeAssertions;
using FocusTray.Infrastructure.Jira;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class JiraOAuthConfigurationTests
{
    [Fact]
    public void Should_BuildApiBaseUrl_When_CloudIdProvided()
    {
        var result = JiraOAuthConfiguration.BuildApiBaseUrl("abc-123");

        result.Should().Be("https://api.atlassian.com/ex/jira/abc-123/");
    }

    [Fact]
    public void Should_Throw_When_BuildApiBaseUrlCalledWithEmptyCloudId()
    {
        var act = () => JiraOAuthConfiguration.BuildApiBaseUrl("");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_IncludeRequiredParameters_When_BuildingAuthorizationUrl()
    {
        var url = JiraOAuthConfiguration.BuildAuthorizationUrl("test-challenge", "test-state");

        url.Should().StartWith("https://auth.atlassian.com/authorize?");
        url.Should().Contain("audience=api.atlassian.com");
        url.Should().Contain($"client_id={JiraOAuthConfiguration.ClientId}");
        url.Should().Contain("code_challenge=test-challenge");
        url.Should().Contain("code_challenge_method=S256");
        url.Should().Contain("state=test-state");
        url.Should().Contain("response_type=code");
        url.Should().Contain(Uri.EscapeDataString(JiraOAuthConfiguration.RedirectUri));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~JiraOAuthConfigurationTests`
Expected: FAIL — compile error, `JiraOAuthConfiguration` does not exist yet.

- [ ] **Step 3: Implement `JiraOAuthConfiguration`**

```csharp
namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Configuration for JIRA OAuth 2.0 (3LO) authentication.
/// ClientId is produced by a manual Atlassian Developer Console app registration
/// (public client + PKCE) — see docs/JIRA_INTEGRATION.md.
/// </summary>
public static class JiraOAuthConfiguration
{
    public const string ClientId = "REPLACE_WITH_ATLASSIAN_OAUTH_CLIENT_ID";

    public const string RedirectUri = "http://localhost:8082/callback";

    public const string AuthorizationEndpoint = "https://auth.atlassian.com/authorize";

    public const string TokenEndpoint = "https://auth.atlassian.com/oauth/token";

    public const string AccessibleResourcesEndpoint = "https://api.atlassian.com/oauth/token/accessible-resources";

    public static readonly string[] Scopes =
    {
        "read:jira-work",
        "write:jira-work",
        "read:jira-user",
        "offline_access"
    };

    public static string BuildApiBaseUrl(string cloudId)
    {
        if (string.IsNullOrWhiteSpace(cloudId))
        {
            throw new ArgumentException("Cloud ID must not be empty.", nameof(cloudId));
        }

        return $"https://api.atlassian.com/ex/jira/{cloudId}/";
    }

    public static string BuildAuthorizationUrl(string codeChallenge, string state)
    {
        var scope = Uri.EscapeDataString(string.Join(' ', Scopes));
        var redirectUri = Uri.EscapeDataString(RedirectUri);

        return $"{AuthorizationEndpoint}?audience=api.atlassian.com&client_id={ClientId}" +
               $"&scope={scope}&redirect_uri={redirectUri}" +
               $"&state={state}&response_type=code&prompt=consent" +
               $"&code_challenge={codeChallenge}&code_challenge_method=S256";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~JiraOAuthConfigurationTests`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add src/FocusTray.Infrastructure/Jira/JiraOAuthConfiguration.cs tests/FocusTray.Tests/Infrastructure/JiraOAuthConfigurationTests.cs
git commit -m "feat(jira): add JIRA OAuth 2.0 configuration and URL builders"
```

---

### Task 2: `IdentityModel` package + `PkceGenerator`

**Files:**
- Modify: `src/FocusTray.Infrastructure/FocusTray.Infrastructure.csproj`
- Create: `src/FocusTray.Infrastructure/Jira/PkceGenerator.cs`
- Test: `tests/FocusTray.Tests/Infrastructure/PkceGeneratorTests.cs`

**Interfaces:**
- Consumes: `IdentityModel.CryptoRandom.CreateUniqueId(int length, CryptoRandom.OutputFormat format)`, `IdentityModel.Base64Url.Encode(byte[])` (from the `IdentityModel` 7.0.0 package).
- Produces: `PkceGenerator.GenerateCodeVerifier() : string`, `PkceGenerator.GenerateState() : string`, `PkceGenerator.GenerateCodeChallenge(string codeVerifier) : string` — used by Task 7.

- [ ] **Step 1: Add the `IdentityModel` package reference**

In `src/FocusTray.Infrastructure/FocusTray.Infrastructure.csproj`, add to the existing `<ItemGroup>` of `PackageReference`s:

```xml
<PackageReference Include="IdentityModel" Version="7.0.0" />
```

- [ ] **Step 2: Write the failing tests**

```csharp
using AwesomeAssertions;
using FocusTray.Infrastructure.Jira;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class PkceGeneratorTests
{
    [Fact]
    public void Should_GenerateVerifierWithinRfcLengthRange_When_GeneratingCodeVerifier()
    {
        var verifier = PkceGenerator.GenerateCodeVerifier();

        verifier.Length.Should().BeInRange(43, 128);
        verifier.Should().MatchRegex("^[A-Za-z0-9_-]+$");
    }

    [Fact]
    public void Should_GenerateDifferentValues_When_CalledTwice()
    {
        var first = PkceGenerator.GenerateCodeVerifier();
        var second = PkceGenerator.GenerateCodeVerifier();

        first.Should().NotBe(second);
    }

    [Fact]
    public void Should_ComputeKnownS256Challenge_When_GivenFixedVerifier()
    {
        // Known-answer test: SHA256("test-code-verifier-1234567890") base64url-encoded,
        // computed independently and verified out-of-band.
        var challenge = PkceGenerator.GenerateCodeChallenge("test-code-verifier-1234567890");

        challenge.Should().Be("zP9WD-aC0tyxQ8j2yFOsEM4jCFnCcGRqEPmHx5qiO70");
    }

    [Fact]
    public void Should_GenerateNonEmptyState_When_GeneratingState()
    {
        var state = PkceGenerator.GenerateState();

        state.Should().NotBeNullOrWhiteSpace();
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~PkceGeneratorTests`
Expected: FAIL — compile error, `PkceGenerator` does not exist yet.

- [ ] **Step 4: Implement `PkceGenerator`**

```csharp
using System.Security.Cryptography;
using System.Text;
using IdentityModel;

namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Generates PKCE (RFC 7636) code verifier/challenge pairs and OAuth state values
/// for the JIRA Authorization Code + PKCE flow.
/// </summary>
public static class PkceGenerator
{
    public static string GenerateCodeVerifier() =>
        CryptoRandom.CreateUniqueId(32, CryptoRandom.OutputFormat.Base64Url);

    public static string GenerateState() =>
        CryptoRandom.CreateUniqueId(16, CryptoRandom.OutputFormat.Base64Url);

    public static string GenerateCodeChallenge(string codeVerifier)
    {
        var verifierBytes = Encoding.ASCII.GetBytes(codeVerifier);
        var hash = SHA256.HashData(verifierBytes);
        return Base64Url.Encode(hash);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~PkceGeneratorTests`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add src/FocusTray.Infrastructure/FocusTray.Infrastructure.csproj src/FocusTray.Infrastructure/Jira/PkceGenerator.cs tests/FocusTray.Tests/Infrastructure/PkceGeneratorTests.cs
git commit -m "feat(jira): add IdentityModel dependency and PKCE generator"
```

---

### Task 3: `JiraTokenCacheHelper` + `JiraTokenCacheData`

**Files:**
- Create: `src/FocusTray.Infrastructure/Jira/JiraTokenCacheHelper.cs`
- Test: `tests/FocusTray.Tests/Infrastructure/JiraTokenCacheHelperTests.cs`

**Interfaces:**
- Produces: `JiraTokenCacheData` (properties: `AccessToken`, `RefreshToken`, `ExpiresAtUtc` (`DateTimeOffset`), `CloudId`, `SiteUrl`, `SiteName`, `Email`, `DisplayName`, all `string` except `ExpiresAtUtc`), `JiraTokenCacheHelper(ILogger? logger = null, string? cacheDirectory = null)`, `.Save(JiraTokenCacheData data)`, `.Load() : JiraTokenCacheData?`, `.Clear()` — used by Task 7.

- [ ] **Step 1: Write the failing tests**

```csharp
using AwesomeAssertions;
using FocusTray.Infrastructure.Jira;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class JiraTokenCacheHelperTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly JiraTokenCacheHelper _helper;

    public JiraTokenCacheHelperTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "FocusTrayTests_" + Guid.NewGuid());
        _helper = new JiraTokenCacheHelper(logger: null, cacheDirectory: _tempDirectory);
    }

    [Fact]
    public void Should_ReturnNull_When_NoCacheFileExists()
    {
        var result = _helper.Load();

        result.Should().BeNull();
    }

    [Fact]
    public void Should_RoundTripData_When_SavingAndLoading()
    {
        var data = new JiraTokenCacheData
        {
            AccessToken = "access-123",
            RefreshToken = "refresh-456",
            ExpiresAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CloudId = "cloud-abc",
            SiteUrl = "https://example.atlassian.net",
            SiteName = "Example",
            Email = "user@example.com",
            DisplayName = "Test User"
        };

        _helper.Save(data);
        var loaded = _helper.Load();

        loaded.Should().NotBeNull();
        loaded!.AccessToken.Should().Be("access-123");
        loaded.RefreshToken.Should().Be("refresh-456");
        loaded.ExpiresAtUtc.Should().Be(data.ExpiresAtUtc);
        loaded.CloudId.Should().Be("cloud-abc");
        loaded.SiteUrl.Should().Be("https://example.atlassian.net");
        loaded.Email.Should().Be("user@example.com");
        loaded.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public void Should_ReturnNull_When_LoadingAfterClear()
    {
        _helper.Save(new JiraTokenCacheData { AccessToken = "x" });
        _helper.Clear();

        var result = _helper.Load();

        result.Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~JiraTokenCacheHelperTests`
Expected: FAIL — compile error, types do not exist yet.

- [ ] **Step 3: Implement `JiraTokenCacheData` and `JiraTokenCacheHelper`**

```csharp
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Cached JIRA OAuth2 token state, persisted encrypted on disk.
/// </summary>
public class JiraTokenCacheData
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string CloudId { get; set; } = string.Empty;
    public string SiteUrl { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Persistent token cache for JIRA OAuth2 tokens, encrypted on disk using Windows DPAPI.
/// Analogous in spirit to MsalTokenCacheHelper, but for JIRA's own token model
/// (Atlassian has no MSAL equivalent).
/// </summary>
public class JiraTokenCacheHelper
{
    private static readonly object FileLock = new();
    private readonly string _cacheFilePath;
    private readonly ILogger? _logger;

    public JiraTokenCacheHelper(ILogger? logger = null, string? cacheDirectory = null)
    {
        _logger = logger;

        var directory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FocusTray");

        Directory.CreateDirectory(directory);
        _cacheFilePath = Path.Combine(directory, "jira_token_cache.dat");
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public void Save(JiraTokenCacheData data)
    {
        lock (FileLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(data);
                var plainBytes = Encoding.UTF8.GetBytes(json);
                var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_cacheFilePath, encryptedBytes);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving JIRA token cache to disk");
            }
        }
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public JiraTokenCacheData? Load()
    {
        lock (FileLock)
        {
            if (!File.Exists(_cacheFilePath))
            {
                return null;
            }

            try
            {
                var encryptedBytes = File.ReadAllBytes(_cacheFilePath);
                var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(plainBytes);
                return JsonSerializer.Deserialize<JiraTokenCacheData>(json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading JIRA token cache from disk; clearing corrupted cache");
                try
                {
                    File.Delete(_cacheFilePath);
                }
                catch
                {
                    // Ignore errors deleting corrupted cache
                }

                return null;
            }
        }
    }

    public void Clear()
    {
        lock (FileLock)
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    File.Delete(_cacheFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error clearing JIRA token cache from disk");
            }
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~JiraTokenCacheHelperTests`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add src/FocusTray.Infrastructure/Jira/JiraTokenCacheHelper.cs tests/FocusTray.Tests/Infrastructure/JiraTokenCacheHelperTests.cs
git commit -m "feat(jira): add encrypted JIRA OAuth2 token cache helper"
```

---

### Task 4: `JiraBearerAuthProvider`

**Files:**
- Create: `src/FocusTray.Infrastructure/Jira/JiraBearerAuthProvider.cs`
- Test: `tests/FocusTray.Tests/Infrastructure/JiraBearerAuthProviderTests.cs`

**Interfaces:**
- Consumes: `Microsoft.Kiota.Abstractions.Authentication.IAuthenticationProvider` (method `Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string,object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)`), `Microsoft.Kiota.Abstractions.RequestInformation` (parameterless `.ctor()`, `.Headers` of type `RequestHeaders` with `TryAdd(string, string) : bool` and `TryGetValue(string, out IEnumerable<string>) : bool`).
- Produces: `JiraBearerAuthProvider(Func<Task<string?>> accessTokenProvider)` implementing `IAuthenticationProvider` — used by Task 9's `JiraService.CreateJiraClient()`.

- [ ] **Step 1: Write the failing tests**

```csharp
using AwesomeAssertions;
using FocusTray.Infrastructure.Jira;
using Microsoft.Kiota.Abstractions;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class JiraBearerAuthProviderTests
{
    [Fact]
    public async Task Should_AddBearerHeader_When_TokenIsAvailable()
    {
        var provider = new JiraBearerAuthProvider(() => Task.FromResult<string?>("fake-token"));
        var request = new RequestInformation();

        await provider.AuthenticateRequestAsync(request);

        request.Headers.TryGetValue("Authorization", out var values).Should().BeTrue();
        values.Should().Contain("Bearer fake-token");
    }

    [Fact]
    public async Task Should_Throw_When_NoTokenIsAvailable()
    {
        var provider = new JiraBearerAuthProvider(() => Task.FromResult<string?>(null));
        var request = new RequestInformation();

        var act = async () => await provider.AuthenticateRequestAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~JiraBearerAuthProviderTests`
Expected: FAIL — compile error, `JiraBearerAuthProvider` does not exist yet.

- [ ] **Step 3: Implement `JiraBearerAuthProvider`**

```csharp
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Kiota authentication provider that attaches a Bearer access token obtained
/// on demand (with silent refresh handled by the delegate's owner, JiraAuthService).
/// </summary>
public class JiraBearerAuthProvider(Func<Task<string?>> accessTokenProvider) : IAuthenticationProvider
{
    private readonly Func<Task<string?>> _accessTokenProvider =
        accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));

    public async Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await _accessTokenProvider();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Cannot authenticate JIRA request: no access token available.");
        }

        request.Headers.TryAdd("Authorization", $"Bearer {accessToken}");
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~JiraBearerAuthProviderTests`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add src/FocusTray.Infrastructure/Jira/JiraBearerAuthProvider.cs tests/FocusTray.Tests/Infrastructure/JiraBearerAuthProviderTests.cs
git commit -m "feat(jira): add Kiota Bearer authentication provider for JIRA client"
```

---

### Task 5: `JiraAccessibleResource` model + `IJiraSitePickerPrompt` interface

**Files:**
- Create: `src/FocusTray.Core/Models/JiraAccessibleResource.cs`
- Create: `src/FocusTray.Core/Services/IJiraSitePickerPrompt.cs`

**Interfaces:**
- Produces: `JiraAccessibleResource { string Id, string Name, string Url }`, `IJiraSitePickerPrompt.PickSiteAsync(IReadOnlyList<JiraAccessibleResource> sites) : Task<JiraAccessibleResource?>` — used by Task 7 (`JiraAuthService`) and Task 10 (`WpfJiraSitePickerPrompt`).

This task has no independent test (it's two plain data/interface declarations with no logic); its correctness is verified by Tasks 7 and 10 compiling and passing against it.

- [ ] **Step 1: Create `JiraAccessibleResource`**

```csharp
namespace FocusTray.Core.Models;

/// <summary>
/// One JIRA Cloud site accessible to the authenticated account
/// (from Atlassian's /oauth/token/accessible-resources endpoint).
/// </summary>
public class JiraAccessibleResource
{
    public string Id { get; set; } = string.Empty; // Atlassian cloudId
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Create `IJiraSitePickerPrompt`**

```csharp
using FocusTray.Core.Models;

namespace FocusTray.Core.Services;

/// <summary>
/// Prompts the user to pick a JIRA site when their account has access to more than one.
/// Implemented in the UI layer (FocusTray project) so that FocusTray.Infrastructure
/// never depends on WPF.
/// </summary>
public interface IJiraSitePickerPrompt
{
    /// <returns>The chosen site, or null if the user cancelled.</returns>
    Task<JiraAccessibleResource?> PickSiteAsync(IReadOnlyList<JiraAccessibleResource> sites);
}
```

- [ ] **Step 3: Build to confirm both compile**

Run: `dotnet build src/FocusTray.Core/FocusTray.Core.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/FocusTray.Core/Models/JiraAccessibleResource.cs src/FocusTray.Core/Services/IJiraSitePickerPrompt.cs
git commit -m "feat(jira): add JiraAccessibleResource model and site picker prompt interface"
```

---

### Task 6: `JiraCallbackListener` (loopback redirect capture)

**Files:**
- Create: `src/FocusTray.Infrastructure/Jira/JiraCallbackListener.cs`
- Test: `tests/FocusTray.Tests/Infrastructure/JiraCallbackListenerTests.cs`

**Interfaces:**
- Produces: `JiraCallbackResult { string? Code, string? State, string? Error, string? ErrorDescription }`, `JiraCallbackListener.ParseCallbackQuery(string query) : JiraCallbackResult` (pure, unit-tested), `JiraCallbackListener.WaitForCallbackAsync(string redirectUri, CancellationToken) : Task<JiraCallbackResult>` (network-bound, manual-tested only per spec) — used by Task 7.

- [ ] **Step 1: Write the failing tests (pure parsing logic only)**

```csharp
using AwesomeAssertions;
using FocusTray.Infrastructure.Jira;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class JiraCallbackListenerTests
{
    [Fact]
    public void Should_ParseCodeAndState_When_QueryContainsSuccessParameters()
    {
        var result = JiraCallbackListener.ParseCallbackQuery("?code=abc123&state=xyz789");

        result.Code.Should().Be("abc123");
        result.State.Should().Be("xyz789");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Should_ParseErrorFields_When_QueryContainsAccessDenied()
    {
        var result = JiraCallbackListener.ParseCallbackQuery(
            "?error=access_denied&error_description=User%20denied%20access&state=xyz789");

        result.Error.Should().Be("access_denied");
        result.ErrorDescription.Should().Be("User denied access");
        result.State.Should().Be("xyz789");
        result.Code.Should().BeNull();
    }

    [Fact]
    public void Should_ReturnEmptyResult_When_QueryIsEmpty()
    {
        var result = JiraCallbackListener.ParseCallbackQuery("");

        result.Code.Should().BeNull();
        result.State.Should().BeNull();
        result.Error.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~JiraCallbackListenerTests`
Expected: FAIL — compile error, `JiraCallbackListener` does not exist yet.

- [ ] **Step 3: Implement `JiraCallbackListener`**

```csharp
using System.Net;
using System.Text;

namespace FocusTray.Infrastructure.Jira;

public class JiraCallbackResult
{
    public string? Code { get; set; }
    public string? State { get; set; }
    public string? Error { get; set; }
    public string? ErrorDescription { get; set; }
}

/// <summary>
/// Captures the OAuth2 authorization redirect on a local loopback HTTP listener
/// (Atlassian has no built-in loopback helper like MSAL provides for Teams).
/// </summary>
public class JiraCallbackListener
{
    public async Task<JiraCallbackResult> WaitForCallbackAsync(string redirectUri, CancellationToken cancellationToken)
    {
        var prefix = redirectUri.EndsWith('/') ? redirectUri : redirectUri + "/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        using var registration = cancellationToken.Register(() => listener.Stop());

        try
        {
            var context = await listener.GetContextAsync();
            var result = ParseCallbackQuery(context.Request.Url!.Query);

            var isSuccess = result.Error == null;
            var responseHtml = isSuccess
                ? "<html><body><h2>Signed in successfully. You can close this tab.</h2></body></html>"
                : "<html><body><h2>Sign-in failed. You can close this tab.</h2></body></html>";

            var buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, CancellationToken.None);
            context.Response.OutputStream.Close();

            return result;
        }
        finally
        {
            listener.Stop();
        }
    }

    public static JiraCallbackResult ParseCallbackQuery(string query)
    {
        var result = new JiraCallbackResult();
        var trimmed = query.TrimStart('?');

        if (string.IsNullOrEmpty(trimmed))
        {
            return result;
        }

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

            switch (key)
            {
                case "code": result.Code = value; break;
                case "state": result.State = value; break;
                case "error": result.Error = value; break;
                case "error_description": result.ErrorDescription = value; break;
            }
        }

        return result;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~JiraCallbackListenerTests`
Expected: PASS (3 tests) — these exercise only `ParseCallbackQuery`; `WaitForCallbackAsync` itself is covered by the manual test procedure documented in Task 14.

- [ ] **Step 5: Commit**

```bash
git add src/FocusTray.Infrastructure/Jira/JiraCallbackListener.cs tests/FocusTray.Tests/Infrastructure/JiraCallbackListenerTests.cs
git commit -m "feat(jira): add OAuth2 loopback callback listener"
```

---

### Task 7: Rewrite `IJiraAuthService` + `JiraAuthService` (full OAuth2 flow)

**Files:**
- Modify: `src/FocusTray.Core/Services/IJiraAuthService.cs`
- Modify: `src/FocusTray.Infrastructure/Jira/JiraAuthService.cs`
- Test: `tests/FocusTray.Tests/Infrastructure/JiraAuthServiceTests.cs` (new file)

**Interfaces:**
- Consumes: `JiraOAuthConfiguration` (Task 1), `PkceGenerator` (Task 2), `JiraTokenCacheHelper`/`JiraTokenCacheData` (Task 3), `JiraAccessibleResource`/`IJiraSitePickerPrompt` (Task 5), `JiraCallbackListener`/`JiraCallbackResult` (Task 6), `ICredentialService` (existing, for one-time legacy cleanup), `IdentityModel.Client` extension methods `HttpClient.RequestAuthorizationCodeTokenAsync(AuthorizationCodeTokenRequest, CancellationToken)`, `HttpClient.RequestRefreshTokenAsync(RefreshTokenRequest, CancellationToken)`, and `TokenResponse { bool IsError, string? Error, string? AccessToken, string? RefreshToken, int ExpiresIn }`.
- Produces: `IJiraAuthService` — `bool IsLoggedIn`, `string? CurrentUsername`, `string? CurrentUserEmail`, `string? CurrentSiteUrl`, `string? CurrentCloudId`, `event EventHandler<AuthStateChangedEventArgs>? AuthStateChanged`, `Task<bool> LoginAsync()`, `Task<bool> LogoutAsync()`, `Task<string?> GetCurrentUserAsync()`, `Task<bool> TryAutoLoginAsync()`, `Task<string?> GetAccessTokenAsync()` — used by Task 9 (`JiraService`), Task 11 (`JiraLoginDialogViewModel`), Task 12 (DI).

This is the largest, most logic-heavy task. Its unit tests focus on the parts that don't require a live browser: state/PKCE validation, token refresh decision logic, and the legacy-credential cleanup — the interactive `LoginAsync()` browser round trip itself is covered by the manual test procedure in Task 14 (same limitation as Teams, which also has no automated interactive-login test).

- [ ] **Step 1: Write the failing tests for the testable parts**

```csharp
using AwesomeAssertions;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using FocusTray.Infrastructure.Jira;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class JiraAuthServiceTests
{
    private readonly Mock<ICredentialService> _mockCredentialService;
    private readonly Mock<IJiraSitePickerPrompt> _mockSitePickerPrompt;
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<JiraAuthService>> _mockLogger;

    public JiraAuthServiceTests()
    {
        _mockCredentialService = new Mock<ICredentialService>();
        _mockCredentialService.Setup(x => x.HasStoredCredentials("FocusTray_Jira")).Returns(false);

        _mockSitePickerPrompt = new Mock<IJiraSitePickerPrompt>();
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _mockLogger = new Mock<ILogger<JiraAuthService>>();
    }

    private JiraAuthService CreateService() =>
        new(_mockCredentialService.Object, _mockSitePickerPrompt.Object, _httpClient, _mockLogger.Object);

    [Fact]
    public void Should_RemoveLegacyCredentials_When_ConstructedWithLegacyBasicAuthStored()
    {
        _mockCredentialService.Setup(x => x.HasStoredCredentials("FocusTray_Jira")).Returns(true);

        _ = CreateService();

        _mockCredentialService.Verify(x => x.DeleteCredentials("FocusTray_Jira"), Times.Once);
    }

    [Fact]
    public void Should_NotDeleteCredentials_When_NoLegacyCredentialsStored()
    {
        _mockCredentialService.Setup(x => x.HasStoredCredentials("FocusTray_Jira")).Returns(false);

        _ = CreateService();

        _mockCredentialService.Verify(x => x.DeleteCredentials(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Should_ReturnFalse_When_TryAutoLoginWithNoStoredCache()
    {
        var service = CreateService();

        var result = await service.TryAutoLoginAsync();

        result.Should().BeFalse();
        service.IsLoggedIn.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReturnNull_When_GetAccessTokenCalledBeforeAnyLogin()
    {
        var service = CreateService();

        var token = await service.GetAccessTokenAsync();

        token.Should().BeNull();
    }

    [Fact]
    public async Task Should_ReturnTrue_When_LogoutCalledWithoutPriorLogin()
    {
        var service = CreateService();

        var result = await service.LogoutAsync();

        result.Should().BeTrue();
        service.IsLoggedIn.Should().BeFalse();
    }

    private void SetupAccessibleResourcesResponse(string json)
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.RequestUri!.ToString() == JiraOAuthConfiguration.AccessibleResourcesEndpoint),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
    }

    [Fact]
    public async Task Should_ReturnSoleSite_When_OnlyOneAccessibleResource()
    {
        SetupAccessibleResourcesResponse(
            "[{\"id\":\"cloud-1\",\"url\":\"https://one.atlassian.net\",\"name\":\"One\"}]");
        var service = CreateService();

        var site = await service.ResolveSiteAsync("fake-token");

        site.Should().NotBeNull();
        site!.Id.Should().Be("cloud-1");
        _mockSitePickerPrompt.Verify(
            x => x.PickSiteAsync(It.IsAny<IReadOnlyList<JiraAccessibleResource>>()), Times.Never);
    }

    [Fact]
    public async Task Should_InvokeSitePicker_When_MultipleAccessibleResources()
    {
        SetupAccessibleResourcesResponse(
            "[{\"id\":\"cloud-1\",\"url\":\"https://one.atlassian.net\",\"name\":\"One\"}," +
            "{\"id\":\"cloud-2\",\"url\":\"https://two.atlassian.net\",\"name\":\"Two\"}]");
        var expectedChoice = new JiraAccessibleResource { Id = "cloud-2", Url = "https://two.atlassian.net", Name = "Two" };
        _mockSitePickerPrompt
            .Setup(x => x.PickSiteAsync(It.Is<IReadOnlyList<JiraAccessibleResource>>(s => s.Count == 2)))
            .ReturnsAsync(expectedChoice);
        var service = CreateService();

        var site = await service.ResolveSiteAsync("fake-token");

        site.Should().BeSameAs(expectedChoice);
        _mockSitePickerPrompt.Verify(
            x => x.PickSiteAsync(It.IsAny<IReadOnlyList<JiraAccessibleResource>>()), Times.Once);
    }

    [Fact]
    public async Task Should_ReturnNull_When_NoAccessibleResources()
    {
        SetupAccessibleResourcesResponse("[]");
        var service = CreateService();

        var site = await service.ResolveSiteAsync("fake-token");

        site.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~JiraAuthServiceTests`
Expected: FAIL — compile error, `IJiraAuthService`/`JiraAuthService` still have the old Basic Auth signatures.

- [ ] **Step 3: Rewrite `IJiraAuthService`**

```csharp
namespace FocusTray.Core.Services;

/// <summary>
/// Service for managing JIRA authentication state via OAuth 2.0 (3LO).
/// </summary>
public interface IJiraAuthService
{
    bool IsLoggedIn { get; }
    string? CurrentUsername { get; }
    string? CurrentUserEmail { get; }
    string? CurrentSiteUrl { get; }
    string? CurrentCloudId { get; }

    event EventHandler<AuthStateChangedEventArgs>? AuthStateChanged;

    /// <summary>
    /// Runs the interactive Authorization Code + PKCE flow via the system browser.
    /// </summary>
    Task<bool> LoginAsync();

    Task<bool> LogoutAsync();

    Task<string?> GetCurrentUserAsync();

    Task<bool> TryAutoLoginAsync();

    /// <summary>
    /// Returns a valid access token, silently refreshing it if it's expired or about to expire.
    /// Returns null if not logged in or refresh fails (caller should treat this as logged out).
    /// </summary>
    Task<string?> GetAccessTokenAsync();
}

public class AuthStateChangedEventArgs : EventArgs
{
    public bool IsLoggedIn { get; }
    public string? Username { get; }

    public AuthStateChangedEventArgs(bool isLoggedIn, string? username)
    {
        IsLoggedIn = isLoggedIn;
        Username = username;
    }
}
```

- [ ] **Step 4: Rewrite `JiraAuthService`**

```csharp
using System.Net.Http.Headers;
using System.Text.Json;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using IdentityModel.Client;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FocusTray.Tests")]

namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Manages JIRA authentication via OAuth 2.0 (3LO) Authorization Code + PKCE.
/// </summary>
public class JiraAuthService : IJiraAuthService
{
    private const string LegacyCredentialTarget = "FocusTray_Jira";

    private readonly ICredentialService _credentialService;
    private readonly IJiraSitePickerPrompt _sitePickerPrompt;
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraAuthService> _logger;
    private readonly JiraTokenCacheHelper _tokenCacheHelper;
    private readonly JiraCallbackListener _callbackListener;

    private JiraTokenCacheData? _cache;
    private bool _isLoggedIn;

    public JiraAuthService(
        ICredentialService credentialService,
        IJiraSitePickerPrompt sitePickerPrompt,
        HttpClient httpClient,
        ILogger<JiraAuthService> logger)
    {
        _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
        _sitePickerPrompt = sitePickerPrompt ?? throw new ArgumentNullException(nameof(sitePickerPrompt));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenCacheHelper = new JiraTokenCacheHelper(logger);
        _callbackListener = new JiraCallbackListener();

        RemoveLegacyBasicAuthCredentials();
    }

    public bool IsLoggedIn => _isLoggedIn;
    public string? CurrentUsername => _cache?.DisplayName;
    public string? CurrentUserEmail => _cache?.Email;
    public string? CurrentSiteUrl => _cache?.SiteUrl;
    public string? CurrentCloudId => _cache?.CloudId;

    public event EventHandler<AuthStateChangedEventArgs>? AuthStateChanged;

    public async Task<bool> LoginAsync()
    {
        try
        {
            var codeVerifier = PkceGenerator.GenerateCodeVerifier();
            var codeChallenge = PkceGenerator.GenerateCodeChallenge(codeVerifier);
            var state = PkceGenerator.GenerateState();
            var authorizationUrl = JiraOAuthConfiguration.BuildAuthorizationUrl(codeChallenge, state);

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var callbackTask = _callbackListener.WaitForCallbackAsync(JiraOAuthConfiguration.RedirectUri, cts.Token);

            _logger.LogInformation("Opening browser for JIRA OAuth2 login");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authorizationUrl,
                UseShellExecute = true
            });

            var callback = await callbackTask;

            if (callback.Error != null)
            {
                _logger.LogWarning("JIRA login denied or failed: {Error} {Description}", callback.Error, callback.ErrorDescription);
                return false;
            }

            if (callback.State != state)
            {
                _logger.LogWarning("JIRA login failed: state mismatch");
                return false;
            }

            if (string.IsNullOrWhiteSpace(callback.Code))
            {
                _logger.LogWarning("JIRA login failed: no authorization code received");
                return false;
            }

            var tokenResponse = await _httpClient.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
            {
                Address = JiraOAuthConfiguration.TokenEndpoint,
                ClientId = JiraOAuthConfiguration.ClientId,
                Code = callback.Code,
                RedirectUri = JiraOAuthConfiguration.RedirectUri,
                CodeVerifier = codeVerifier
            });

            if (tokenResponse.IsError || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                _logger.LogWarning("JIRA token exchange failed: {Error}", tokenResponse.Error);
                return false;
            }

            var site = await ResolveSiteAsync(tokenResponse.AccessToken);
            if (site == null)
            {
                _logger.LogWarning("JIRA login failed: no accessible JIRA site for this account");
                return false;
            }

            var (email, displayName) = await GetMyselfAsync(tokenResponse.AccessToken, site.Id);

            _cache = new JiraTokenCacheData
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken ?? string.Empty,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                CloudId = site.Id,
                SiteUrl = site.Url,
                SiteName = site.Name,
                Email = email ?? string.Empty,
                DisplayName = displayName ?? email ?? site.Name
            };

            _tokenCacheHelper.Save(_cache);
            _isLoggedIn = true;

            _logger.LogInformation("Successfully logged in to JIRA as {User} on site {Site}", _cache.DisplayName, _cache.SiteUrl);
            OnAuthStateChanged(new AuthStateChangedEventArgs(true, _cache.DisplayName));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during JIRA login");
            return false;
        }
    }

    public Task<bool> LogoutAsync()
    {
        _tokenCacheHelper.Clear();
        _cache = null;
        _isLoggedIn = false;

        _logger.LogInformation("Logged out from JIRA");
        OnAuthStateChanged(new AuthStateChangedEventArgs(false, null));

        return Task.FromResult(true);
    }

    public async Task<string?> GetCurrentUserAsync()
    {
        var accessToken = await GetAccessTokenAsync();
        if (accessToken == null || _cache == null)
        {
            return null;
        }

        var (_, displayName) = await GetMyselfAsync(accessToken, _cache.CloudId);
        return displayName ?? _cache.DisplayName;
    }

    public async Task<bool> TryAutoLoginAsync()
    {
        _cache = _tokenCacheHelper.Load();
        if (_cache == null)
        {
            _logger.LogInformation("No stored JIRA token cache found for auto-login");
            return false;
        }

        var accessToken = await GetAccessTokenAsync();
        if (accessToken == null)
        {
            _logger.LogWarning("JIRA auto-login failed: could not obtain a valid access token");
            return false;
        }

        _isLoggedIn = true;
        _logger.LogInformation("JIRA auto-login successful as {User}", _cache.DisplayName);
        OnAuthStateChanged(new AuthStateChangedEventArgs(true, _cache.DisplayName));
        return true;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (_cache == null)
        {
            return null;
        }

        if (_cache.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return _cache.AccessToken;
        }

        try
        {
            var response = await _httpClient.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                Address = JiraOAuthConfiguration.TokenEndpoint,
                ClientId = JiraOAuthConfiguration.ClientId,
                RefreshToken = _cache.RefreshToken
            });

            if (response.IsError || string.IsNullOrWhiteSpace(response.AccessToken))
            {
                _logger.LogWarning("JIRA token refresh failed: {Error}", response.Error);
                _isLoggedIn = false;
                _tokenCacheHelper.Clear();
                _cache = null;
                OnAuthStateChanged(new AuthStateChangedEventArgs(false, null));
                return null;
            }

            _cache.AccessToken = response.AccessToken;
            _cache.RefreshToken = response.RefreshToken ?? _cache.RefreshToken;
            _cache.ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn);
            _tokenCacheHelper.Save(_cache);

            return _cache.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing JIRA access token");
            return null;
        }
    }

    // Internal (not private) + InternalsVisibleTo below so JiraAuthServiceTests can
    // unit-test the single/multi/zero-site decision logic without a live browser flow.
    internal async Task<JiraAccessibleResource?> ResolveSiteAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, JiraOAuthConfiguration.AccessibleResourcesEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to retrieve accessible JIRA resources: {StatusCode}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var resources = JsonSerializer.Deserialize<List<AccessibleResourceDto>>(json, JsonOptions) ?? new List<AccessibleResourceDto>();

        var sites = resources
            .Where(r => !string.IsNullOrEmpty(r.Id))
            .Select(r => new JiraAccessibleResource { Id = r.Id!, Name = r.Name ?? r.Url ?? r.Id!, Url = r.Url ?? string.Empty })
            .ToList();

        if (sites.Count == 0)
        {
            return null;
        }

        if (sites.Count == 1)
        {
            return sites[0];
        }

        return await _sitePickerPrompt.PickSiteAsync(sites);
    }

    private async Task<(string? Email, string? DisplayName)> GetMyselfAsync(string accessToken, string cloudId)
    {
        var baseUrl = JiraOAuthConfiguration.BuildApiBaseUrl(cloudId);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}rest/api/2/myself");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return (null, null);
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var email = doc.RootElement.TryGetProperty("emailAddress", out var e) ? e.GetString() : null;
        var displayName = doc.RootElement.TryGetProperty("displayName", out var d) ? d.GetString() : null;
        return (email, displayName);
    }

    private void RemoveLegacyBasicAuthCredentials()
    {
        if (_credentialService.HasStoredCredentials(LegacyCredentialTarget))
        {
            _logger.LogInformation("Removing legacy JIRA Basic Auth credentials; re-login via OAuth2 is required");
            _credentialService.DeleteCredentials(LegacyCredentialTarget);
        }
    }

    protected virtual void OnAuthStateChanged(AuthStateChangedEventArgs e)
    {
        AuthStateChanged?.Invoke(this, e);
    }

    // Atlassian returns lowercase JSON keys (id/url/name); match case-insensitively
    // rather than relying on exact PascalCase-to-lowercase default binding.
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private class AccessibleResourceDto
    {
        public string? Id { get; set; }
        public string? Url { get; set; }
        public string? Name { get; set; }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~JiraAuthServiceTests`
Expected: PASS (5 tests). Note: this will also break `JiraServiceTests` and `JiraServiceIntegrationTests` compilation until Tasks 9 and 13 update them — that's expected and resolved in those tasks; do not attempt to fix them here.

- [ ] **Step 6: Commit**

```bash
git add src/FocusTray.Core/Services/IJiraAuthService.cs src/FocusTray.Infrastructure/Jira/JiraAuthService.cs tests/FocusTray.Tests/Infrastructure/JiraAuthServiceTests.cs
git commit -m "feat(jira): replace Basic Auth with OAuth 2.0 Authorization Code + PKCE flow"
```

---

### Task 8: `JiraConfiguration` — remove `Company`

**Files:**
- Modify: `src/FocusTray.Infrastructure/Jira/JiraConfiguration.cs`

**Interfaces:**
- Produces: `JiraConfiguration { string JqlFilter }` (no more `Company`) — used by Task 9.

- [ ] **Step 1: Remove the `Company` property**

```csharp
namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Configuration settings for JIRA integration.
/// </summary>
public class JiraConfiguration
{
    /// <summary>
    /// Gets or sets the JQL query to fetch assigned issues.
    /// Default: "assignee = currentUser() AND statusCategory != Done"
    /// </summary>
    public string JqlFilter { get; set; } = "assignee = currentUser() AND statusCategory != Done";
}
```

- [ ] **Step 2: Build to confirm compilation (failures expected downstream until Task 9/13)**

Run: `dotnet build src/FocusTray.Infrastructure/FocusTray.Infrastructure.csproj`
Expected: Build succeeded for this project (it no longer references `Company` anywhere within itself after Task 9's client rewrite lands — if Task 9 has not yet landed, `JiraService.cs` will still fail to compile referencing `Company`/`CurrentCompany`; that's expected and resolved by Task 9).

- [ ] **Step 3: Commit**

```bash
git add src/FocusTray.Infrastructure/Jira/JiraConfiguration.cs
git commit -m "feat(jira): remove Company from JiraConfiguration (superseded by OAuth2 site resolution)"
```

---

### Task 9: Rewrite `JiraService.CreateJiraClient()` + update `JiraServiceTests`

**Files:**
- Modify: `src/FocusTray.Infrastructure/Jira/JiraService.cs`
- Modify: `tests/FocusTray.Tests/Infrastructure/JiraServiceTests.cs`

**Interfaces:**
- Consumes: `JiraOAuthConfiguration.BuildApiBaseUrl` (Task 1), `JiraBearerAuthProvider` (Task 4), `IJiraAuthService.CurrentCloudId`/`.GetAccessTokenAsync()` (Task 7), `Kubis1982.Atlassian.RestClient.HttpClientRequestAdapterFactory.Create(string baseUrl, IAuthenticationProvider authenticationProvider, HttpClient httpClient) : HttpClientRequestAdapter`, `new JiraRestClient(IRequestAdapter)` (public constructor, confirmed via reflection on `Kubis1982.Atlassian.Jira.RestClient.V2.dll` 1.8464.1).
- Produces: `JiraService(IJiraAuthService authService, JiraConfiguration configuration, HttpClient httpClient, ILogger<JiraService> logger)` — the `ICredentialService` constructor parameter is removed (no longer needed).

- [ ] **Step 1: Update `JiraServiceTests.cs` (failing first)**

Replace the constructor/mocks and the two assertions that reference the old auth model:

```csharp
using AwesomeAssertions;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using FocusTray.Infrastructure.Jira;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Xunit;

namespace FocusTray.Tests.Infrastructure;

public class JiraServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<JiraService>> _mockLogger;
    private readonly Mock<IJiraAuthService> _mockAuthService;
    private readonly JiraConfiguration _configuration;
    private readonly JiraService _jiraService;

    public JiraServiceTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _mockLogger = new Mock<ILogger<JiraService>>();
        _mockAuthService = new Mock<IJiraAuthService>();

        _configuration = new JiraConfiguration
        {
            JqlFilter = "assignee = currentUser() AND statusCategory != Done"
        };

        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(true);
        _mockAuthService.Setup(x => x.CurrentCloudId).Returns("test-cloud-id");
        _mockAuthService.Setup(x => x.CurrentUsername).Returns("Test User");
        _mockAuthService.Setup(x => x.GetAccessTokenAsync()).ReturnsAsync("test-access-token");

        _jiraService = new JiraService(_mockAuthService.Object, _configuration, _httpClient, _mockLogger.Object);
    }

    [Fact]
    public void Should_ReturnEnabled_When_UserIsLoggedIn()
    {
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(true);

        var isEnabled = _jiraService.IsEnabled;

        isEnabled.Should().BeTrue();
    }

    [Fact]
    public void Should_ReturnDisabled_When_UserIsNotLoggedIn()
    {
        _mockAuthService.Setup(x => x.IsLoggedIn).Returns(false);
        var service = new JiraService(_mockAuthService.Object, _configuration, _httpClient, _mockLogger.Object);

        var isEnabled = service.IsEnabled;

        isEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReturnTrue_When_ConnectionTestSucceeds()
    {
        var responseContent = JsonSerializer.Serialize(new
        {
            accountId = "123",
            emailAddress = "test@example.com",
            displayName = "Test User",
            active = true
        });
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
            });

        var result = await _jiraService.TestConnectionAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnFalse_When_ConnectionTestFails()
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.Unauthorized });

        var result = await _jiraService.TestConnectionAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_ReturnIssues_When_GetAssignedIssuesSucceeds()
    {
        var searchResponse = new
        {
            issues = new[]
            {
                new { key = "PROJ-1", fields = new { summary = "First issue", issuetype = new { name = "Task" }, status = new { name = "In Progress" } } },
                new { key = "PROJ-2", fields = new { summary = "Second issue", issuetype = new { name = "Bug" }, status = new { name = "To Do" } } }
            }
        };

        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/rest/api/2/search/jql")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(searchResponse), System.Text.Encoding.UTF8, "application/json")
            });

        var issues = await _jiraService.GetAssignedIssuesAsync();

        issues.Should().HaveCount(2);
        issues[0].Key.Should().Be("PROJ-1");
        issues[1].Key.Should().Be("PROJ-2");
    }

    [Fact]
    public async Task Should_ReturnEmptyList_When_GetAssignedIssuesFails()
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest, Content = new StringContent("Bad Request") });

        var issues = await _jiraService.GetAssignedIssuesAsync();

        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_ReturnTrue_When_AddWorklogSucceeds()
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("/rest/api/2/issue/") &&
                    req.RequestUri!.ToString().Contains("/worklog")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Created,
                Content = new StringContent("{\"id\": \"10000\"}", System.Text.Encoding.UTF8, "application/json")
            });

        var worklog = new JiraWorklog { IssueKey = "PROJ-1", TimeSpentSeconds = 3600, Comment = "Worked on implementation", Started = DateTime.UtcNow };

        var result = await _jiraService.AddWorklogAsync(worklog);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Should_ReturnFalse_When_AddWorklogFails()
    {
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest });

        var worklog = new JiraWorklog { IssueKey = "PROJ-1", TimeSpentSeconds = 3600, Comment = "Test", Started = DateTime.UtcNow };

        var result = await _jiraService.AddWorklogAsync(worklog);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_IncludeBearerAuthorizationHeader_When_MakingRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{\"accountId\": \"123\"}") });

        await _jiraService.TestConnectionAsync();

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization!.Parameter.Should().Be("test-access-token");
    }

    [Fact]
    public async Task Should_UseConfiguredJqlFilter_When_GetAssignedIssues()
    {
        HttpRequestMessage? capturedRequest = null;
        _mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("/rest/api/2/search/jql")),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{\"issues\": []}", System.Text.Encoding.UTF8, "application/json") });

        await _jiraService.GetAssignedIssuesAsync();

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.Query.Should().Contain("jql=");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~JiraServiceTests`
Expected: FAIL — `JiraService` constructor still requires `ICredentialService` and builds a Basic Auth client.

- [ ] **Step 3: Rewrite `JiraService.cs`**

```csharp
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using Kubis1982.Atlassian.Jira.RestClient.V2;
using Kubis1982.Atlassian.Jira.RestClient.V2.Models;
using Kubis1982.Atlassian.RestClient;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions.Serialization;

namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Service for interacting with JIRA Atlassian Cloud via REST API.
/// </summary>
public class JiraService : IJiraService
{
    private readonly IJiraAuthService _authService;
    private readonly JiraConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<JiraService> _logger;

    public JiraService(
        IJiraAuthService authService,
        JiraConfiguration configuration,
        HttpClient httpClient,
        ILogger<JiraService> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsEnabled => _authService.IsLoggedIn;

    public async Task<bool> TestConnectionAsync()
    {
        if (!_authService.IsLoggedIn)
        {
            _logger.LogWarning("JIRA user is not logged in");
            return false;
        }

        try
        {
            var jiraClient = CreateJiraClient();
            if (jiraClient == null)
            {
                return false;
            }

            _logger.LogInformation("Testing JIRA connection to {Site}", _authService.CurrentSiteUrl);

            var user = await jiraClient.Rest.Api.Two.Myself.GetAsync();

            if (user != null)
            {
                _logger.LogInformation("JIRA connection test successful");
                return true;
            }

            _logger.LogWarning("JIRA connection test failed: user is null");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing JIRA connection");
            return false;
        }
    }

    public async Task<IReadOnlyList<JiraIssue>> GetAssignedIssuesAsync()
    {
        if (!_authService.IsLoggedIn)
        {
            _logger.LogWarning("JIRA user is not logged in");
            return Array.Empty<JiraIssue>();
        }

        try
        {
            var jiraClient = CreateJiraClient();
            if (jiraClient == null)
            {
                return Array.Empty<JiraIssue>();
            }

            _logger.LogInformation("Fetching assigned JIRA issues with JQL: {JQL}", _configuration.JqlFilter);

            var searchResponse = await jiraClient.Rest.Api.Two.Search.Jql.GetAsync(q =>
            {
                q.QueryParameters.Jql = _configuration.JqlFilter;
                q.QueryParameters.Fields = new[] { "key", "summary", "issuetype", "status" };
                q.QueryParameters.MaxResults = 100;
            });

            if (searchResponse?.Issues == null)
            {
                _logger.LogWarning("No issues returned from JIRA");
                return Array.Empty<JiraIssue>();
            }

            var issues = searchResponse.Issues
                .Select(issue =>
                {
                    var fields = issue.Fields?.AdditionalData;
                    var summary = fields?.TryGetValue("summary", out var summaryObj) == true ? summaryObj?.ToString() : null;
                    var issueType = fields?.TryGetValue("issuetype", out var issueTypeObj) == true && issueTypeObj is IParsable issueTypeParsable
                        ? (issueTypeParsable as IssueTypeDetails)?.Name ?? "Unknown"
                        : "Unknown";
                    var status = fields?.TryGetValue("status", out var statusObj) == true && statusObj is IParsable statusParsable
                        ? (statusParsable as StatusDetails)?.Name ?? "Unknown"
                        : "Unknown";

                    return new JiraIssue
                    {
                        Key = issue.Key!,
                        Summary = summary!,
                        IssueType = issueType,
                        Status = status
                    };
                })
                .ToList();

            _logger.LogInformation("Retrieved {Count} JIRA issues", issues.Count);
            return issues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching JIRA issues");
            return Array.Empty<JiraIssue>();
        }
    }

    public async Task<bool> AddWorklogAsync(JiraWorklog worklog)
    {
        if (!_authService.IsLoggedIn)
        {
            _logger.LogWarning("JIRA user is not logged in");
            return false;
        }

        if (string.IsNullOrWhiteSpace(worklog.IssueKey))
        {
            _logger.LogWarning("Cannot add worklog: issue key is empty");
            return false;
        }

        try
        {
            var jiraClient = CreateJiraClient();
            if (jiraClient == null)
            {
                return false;
            }

            _logger.LogInformation(
                "Adding worklog to {IssueKey}: {Seconds}s on site {Site}",
                worklog.IssueKey,
                worklog.TimeSpentSeconds,
                _authService.CurrentSiteUrl);

            var worklogRequest = new Worklog
            {
                TimeSpentSeconds = worklog.TimeSpentSeconds,
                Started = worklog.Started,
                Comment = worklog.Comment
            };

            await jiraClient.Rest.Api.Two.Issue[worklog.IssueKey].Worklog.PostAsync(worklogRequest);

            _logger.LogInformation("Worklog added successfully to {IssueKey}", worklog.IssueKey);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding worklog to {IssueKey}", worklog.IssueKey);
            return false;
        }
    }

    private JiraRestClient? CreateJiraClient()
    {
        try
        {
            var cloudId = _authService.CurrentCloudId;
            if (string.IsNullOrWhiteSpace(cloudId))
            {
                _logger.LogWarning("Cannot create JIRA client: not logged in (no cloud ID)");
                return null;
            }

            var baseUrl = JiraOAuthConfiguration.BuildApiBaseUrl(cloudId);
            var authProvider = new JiraBearerAuthProvider(() => _authService.GetAccessTokenAsync());
            var requestAdapter = HttpClientRequestAdapterFactory.Create(baseUrl, authProvider, _httpClient);

            return new JiraRestClient(requestAdapter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating JIRA client");
            return null;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/FocusTray.Tests --filter FullyQualifiedName~JiraServiceTests`
Expected: PASS (10 tests)

- [ ] **Step 5: Commit**

```bash
git add src/FocusTray.Infrastructure/Jira/JiraService.cs tests/FocusTray.Tests/Infrastructure/JiraServiceTests.cs
git commit -m "feat(jira): build JIRA REST client from Bearer token + cloudId instead of Basic Auth"
```

---

### Task 10: `JiraSitePickerDialog` (ViewModel + View) + `WpfJiraSitePickerPrompt`

**Files:**
- Create: `src/FocusTray/ViewModels/JiraSitePickerDialogViewModel.cs`
- Create: `src/FocusTray/Views/JiraSitePickerDialog.xaml`
- Create: `src/FocusTray/Views/JiraSitePickerDialog.xaml.cs`
- Create: `src/FocusTray/Services/WpfJiraSitePickerPrompt.cs`

**Interfaces:**
- Consumes: `JiraAccessibleResource` (Task 5), `IJiraSitePickerPrompt` (Task 5).
- Produces: `WpfJiraSitePickerPrompt : IJiraSitePickerPrompt` — used by Task 12 (DI registration) and, transitively, Task 7's `JiraAuthService.ResolveSiteAsync`.

This task is UI-only and has no automated test (WPF dialogs in this codebase are manually tested — see the existing `JiraLoginDialog`/`TeamsLoginDialog`, neither of which has a unit test either). Verify it via the manual test procedure in Task 14.

- [ ] **Step 1: Create `JiraSitePickerDialogViewModel`**

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusTray.Core.Models;

namespace FocusTray.ViewModels;

public partial class JiraSitePickerDialogViewModel : ObservableObject
{
    public ObservableCollection<JiraAccessibleResource> Sites { get; }

    [ObservableProperty]
    private JiraAccessibleResource? _selectedSite;

    public JiraSitePickerDialogViewModel(IReadOnlyList<JiraAccessibleResource> sites)
    {
        Sites = new ObservableCollection<JiraAccessibleResource>(sites);
        SelectedSite = Sites.FirstOrDefault();
    }
}
```

- [ ] **Step 2: Create `JiraSitePickerDialog.xaml`**

```xml
<Window x:Class="FocusTray.Views.JiraSitePickerDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Title="Choose a JIRA site"
        Height="Auto"
        SizeToContent="Height"
        Width="450"
        WindowStartupLocation="CenterScreen"
        ResizeMode="NoResize">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0"
                   Text="Your account has access to multiple JIRA sites"
                   FontSize="16"
                   FontWeight="Bold"
                   TextWrapping="Wrap"
                   Margin="0,0,0,15"/>

        <TextBlock Grid.Row="1"
                   Text="Choose which one FocusTray should connect to:"
                   Margin="0,0,0,10"/>

        <ListBox Grid.Row="2"
                 Name="SitesListBox"
                 MaxHeight="200"
                 Margin="0,0,0,15"
                 DisplayMemberPath="Name"/>

        <StackPanel Grid.Row="3"
                    Orientation="Horizontal"
                    HorizontalAlignment="Right">
            <Button Name="OkButton"
                    Content="OK"
                    Width="100"
                    Height="35"
                    Click="Ok_Click"
                    Margin="0,0,10,0"
                    IsDefault="True"/>
            <Button Name="CancelButton"
                    Content="Cancel"
                    Width="100"
                    Height="35"
                    Click="Cancel_Click"
                    IsCancel="True"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 3: Create `JiraSitePickerDialog.xaml.cs`**

```csharp
using System.Windows;
using FocusTray.ViewModels;

namespace FocusTray.Views;

public partial class JiraSitePickerDialog : Window
{
    private readonly JiraSitePickerDialogViewModel _viewModel;

    public JiraSitePickerDialog(JiraSitePickerDialogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        SitesListBox.ItemsSource = _viewModel.Sites;
        SitesListBox.SelectedItem = _viewModel.SelectedSite;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedSite = SitesListBox.SelectedItem as Core.Models.JiraAccessibleResource;
        DialogResult = _viewModel.SelectedSite != null;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
```

- [ ] **Step 4: Create `WpfJiraSitePickerPrompt`**

```csharp
using System.Windows;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using FocusTray.ViewModels;
using FocusTray.Views;

namespace FocusTray.Services;

/// <summary>
/// WPF implementation of the JIRA site picker prompt, shown when an account
/// has access to more than one JIRA Cloud site.
/// </summary>
public class WpfJiraSitePickerPrompt : IJiraSitePickerPrompt
{
    public Task<JiraAccessibleResource?> PickSiteAsync(IReadOnlyList<JiraAccessibleResource> sites)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var viewModel = new JiraSitePickerDialogViewModel(sites);
            var dialog = new JiraSitePickerDialog(viewModel);
            var result = dialog.ShowDialog();
            return result == true ? viewModel.SelectedSite : null;
        }).Task;
    }
}
```

Note: `JiraSitePickerDialogViewModel` and `JiraSitePickerDialog` are constructed with `new` here, not resolved via DI — they need a per-call `sites` list that the DI container can't provide generically. Do **not** register them in `App.xaml.cs`.

- [ ] **Step 5: Build to confirm compilation**

Run: `dotnet build src/FocusTray/FocusTray.csproj`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/FocusTray/ViewModels/JiraSitePickerDialogViewModel.cs src/FocusTray/Views/JiraSitePickerDialog.xaml src/FocusTray/Views/JiraSitePickerDialog.xaml.cs src/FocusTray/Services/WpfJiraSitePickerPrompt.cs
git commit -m "feat(jira): add JIRA site picker dialog for multi-site accounts"
```

---

### Task 11: Simplify `JiraLoginDialogViewModel` + `JiraLoginDialog`

**Files:**
- Modify: `src/FocusTray/ViewModels/JiraLoginDialogViewModel.cs`
- Modify: `src/FocusTray/Views/JiraLoginDialog.xaml`
- Modify: `src/FocusTray/Views/JiraLoginDialog.xaml.cs`

**Interfaces:**
- Consumes: `IJiraAuthService.LoginAsync()` (Task 7, now parameterless).

No new automated test — mirrors the existing, untested `TeamsLoginDialogViewModel`/`TeamsLoginDialog` pattern exactly. Verified via the manual test procedure in Task 14.

- [ ] **Step 1: Rewrite `JiraLoginDialogViewModel.cs`**

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

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _isLoading;

    [RelayCommand]
    private async Task LoginAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Signing in... Please complete authentication in your browser.";
            IsSuccess = false;

            var success = await _authService.LoginAsync();

            if (success)
            {
                StatusMessage = $"Successfully signed in to JIRA as {_authService.CurrentUsername}!";
                IsSuccess = true;
            }
            else
            {
                StatusMessage = "Sign in failed. Please try again.";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
```

- [ ] **Step 2: Rewrite `JiraLoginDialog.xaml`** (mirrors `TeamsLoginDialog.xaml`)

```xml
<Window x:Class="FocusTray.Views.JiraLoginDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Title="Login to JIRA"
        Height="Auto"
        SizeToContent="Height"
        Width="500"
        WindowStartupLocation="CenterScreen"
        ResizeMode="NoResize">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0"
                   Text="Login to JIRA"
                   FontSize="20"
                   FontWeight="Bold"
                   Margin="0,0,0,20"/>

        <TextBlock Grid.Row="1"
                   TextWrapping="Wrap"
                   Margin="0,0,0,20">
            <Run Text="Click the button below to sign in with your Atlassian account."/>
            <LineBreak/>
            <Run Text="A browser window will open for authentication."/>
        </TextBlock>

        <TextBlock Grid.Row="2"
                   Name="StatusTextBlock"
                   TextWrapping="Wrap"
                   Margin="0,0,0,15"
                   MinHeight="20"/>

        <ProgressBar Grid.Row="3"
                     Name="ProgressBar"
                     Height="5"
                     IsIndeterminate="True"
                     Margin="0,0,0,15"
                     Visibility="Collapsed"/>

        <StackPanel Grid.Row="4"
                    Orientation="Horizontal"
                    HorizontalAlignment="Right">
            <Button Name="LoginButton"
                    Content="Sign in with Atlassian"
                    Width="180"
                    Height="35"
                    Click="Login_Click"
                    Margin="0,0,10,0"/>
            <Button Name="CancelButton"
                    Content="Cancel"
                    Width="100"
                    Height="35"
                    Click="Cancel_Click"
                    IsCancel="True"/>
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 3: Rewrite `JiraLoginDialog.xaml.cs`** (mirrors `TeamsLoginDialog.xaml.cs`)

```csharp
using System.Windows;
using FocusTray.ViewModels;

namespace FocusTray.Views;

public partial class JiraLoginDialog : Window
{
    private readonly JiraLoginDialogViewModel _viewModel;

    public JiraLoginDialog(JiraLoginDialogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        StatusTextBlock.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
            new System.Windows.Data.Binding(nameof(_viewModel.StatusMessage))
            {
                Source = _viewModel
            });

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsSuccess))
            {
                StatusTextBlock.Foreground = _viewModel.IsSuccess
                    ? System.Windows.Media.Brushes.Green
                    : System.Windows.Media.Brushes.Red;
            }
            else if (e.PropertyName == nameof(_viewModel.StatusMessage) &&
                     _viewModel.StatusMessage.Contains("Signing in"))
            {
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Blue;
            }
            else if (e.PropertyName == nameof(_viewModel.IsLoading))
            {
                LoginButton.IsEnabled = !_viewModel.IsLoading;
                CancelButton.IsEnabled = !_viewModel.IsLoading;
                ProgressBar.Visibility = _viewModel.IsLoading
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        };
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoginCommand.ExecuteAsync(null);

        if (_viewModel.IsSuccess)
        {
            await Task.Delay(1500);
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
```

- [ ] **Step 4: Build to confirm compilation**

Run: `dotnet build src/FocusTray/FocusTray.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/FocusTray/ViewModels/JiraLoginDialogViewModel.cs src/FocusTray/Views/JiraLoginDialog.xaml src/FocusTray/Views/JiraLoginDialog.xaml.cs
git commit -m "feat(jira): simplify JIRA login dialog to a single browser sign-in button"
```

---

### Task 12: DI wiring in `App.xaml.cs`

**Files:**
- Modify: `src/FocusTray/App.xaml.cs`

**Interfaces:**
- Consumes: `IJiraSitePickerPrompt`/`WpfJiraSitePickerPrompt` (Task 10), `IJiraAuthService`/`JiraAuthService` (Task 7).

- [ ] **Step 1: Register `IJiraSitePickerPrompt` before the JIRA auth service registration**

In `OnStartup`, immediately before the existing line `services.AddSingleton<IJiraAuthService, JiraAuthService>();`, add:

```csharp
// Add JIRA site picker prompt (WPF implementation of a Core interface, so
// FocusTray.Infrastructure never depends on WPF)
services.AddSingleton<IJiraSitePickerPrompt, Services.WpfJiraSitePickerPrompt>();
```

Add the missing `using FocusTray.Core.Services;` import if not already present (it already is, per the existing file's first `using` line).

- [ ] **Step 2: Build the full solution**

Run: `dotnet build FocusTray.sln`
Expected: Build succeeded — `JiraAuthService`'s constructor now resolves `IJiraSitePickerPrompt` from DI.

- [ ] **Step 3: Commit**

```bash
git add src/FocusTray/App.xaml.cs
git commit -m "feat(jira): register WPF JIRA site picker prompt in DI"
```

---

### Task 13: Rewrite `JiraServiceIntegrationTests`

**Files:**
- Modify: `tests/FocusTray.IntegrationTests/Jira/JiraServiceIntegrationTests.cs`

**Interfaces:**
- Consumes: `IJiraAuthService` (Task 7's new shape), `JiraConfiguration` (Task 8).

Full interactive OAuth2 login can't run unattended in CI (same limitation Teams already has — no automated interactive-login integration test exists for it either). These integration tests now authenticate with a **pre-obtained** access token + cloud ID (produced once via the real interactive flow — see Task 14's manual procedure — and stored as environment variables for local/CI runs), instead of a company/email/API token triple.

- [ ] **Step 1: Rewrite the test file**

```csharp
using AwesomeAssertions;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using FocusTray.Infrastructure.Jira;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FocusTray.IntegrationTests.Jira;

/// <summary>
/// Integration tests for JiraService against a real JIRA Cloud site.
/// These tests require a JIRA OAuth2 access token obtained via the real interactive
/// login flow (see docs/JIRA_INTEGRATION.md "Manual OAuth2 test procedure"), exposed via:
/// - JIRA_ACCESS_TOKEN (a currently-valid OAuth2 access token)
/// - JIRA_CLOUD_ID (the Atlassian cloudId for the target site)
///
/// Tests are skipped if environment variables are not set.
/// </summary>
public class JiraServiceIntegrationTests : IDisposable
{
    private readonly JiraService? _jiraService;
    private readonly HttpClient _httpClient;
    private readonly bool _isConfigured;
    private readonly string _skipReason;

    public JiraServiceIntegrationTests()
    {
        _httpClient = new HttpClient();

        var accessToken = Environment.GetEnvironmentVariable("JIRA_ACCESS_TOKEN");
        var cloudId = Environment.GetEnvironmentVariable("JIRA_CLOUD_ID");

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(cloudId))
        {
            _isConfigured = false;
            _skipReason = "JIRA environment variables not configured. Set JIRA_ACCESS_TOKEN and JIRA_CLOUD_ID to run integration tests.";
            return;
        }

        _isConfigured = true;
        _skipReason = string.Empty;

        var authService = new TestJiraAuthService(accessToken, cloudId);

        var configuration = new JiraConfiguration
        {
            JqlFilter = "assignee = currentUser() AND statusCategory != Done ORDER BY updated DESC"
        };

        var logger = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning))
            .CreateLogger<JiraService>();

        _jiraService = new JiraService(authService, configuration, _httpClient, logger);
    }

    [SkippableFact]
    public async Task Should_ConnectToJira_When_AccessTokenIsValid()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        var result = await _jiraService!.TestConnectionAsync();

        result.Should().BeTrue("connection test should succeed with a valid access token");
    }

    [SkippableFact]
    public async Task Should_RetrieveAssignedIssues_When_CallingGetAssignedIssues()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        var issues = await _jiraService!.GetAssignedIssuesAsync();

        issues.Should().NotBeNull("service should return a list");
        if (issues.Count > 0)
        {
            var firstIssue = issues[0];
            firstIssue.Key.Should().NotBeNullOrWhiteSpace("issue key should be populated");
            firstIssue.Summary.Should().NotBeNullOrWhiteSpace("issue summary should be populated");
            firstIssue.Status.Should().NotBeNullOrWhiteSpace("issue status should be populated");
        }
    }

    [SkippableFact]
    public async Task Should_AddWorklog_When_ValidIssueKeyProvided()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        var issues = await _jiraService!.GetAssignedIssuesAsync();

        Skip.If(issues.Count == 0, "No assigned issues available to test worklog creation");

        var testIssue = issues[0];
        var worklog = new JiraWorklog
        {
            IssueKey = testIssue.Key,
            TimeSpentSeconds = 300,
            Comment = "FocusTray Integration Test - Auto-generated worklog entry",
            Started = DateTime.UtcNow.AddMinutes(-5)
        };

        var result = await _jiraService.AddWorklogAsync(worklog);

        result.Should().BeTrue($"worklog should be added successfully to issue {testIssue.Key}");
    }

    [SkippableFact]
    public async Task Should_ReturnFalse_When_AddingWorklogToNonExistentIssue()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        var worklog = new JiraWorklog
        {
            IssueKey = "NONEXISTENT-99999",
            TimeSpentSeconds = 300,
            Comment = "This should fail",
            Started = DateTime.UtcNow
        };

        var result = await _jiraService!.AddWorklogAsync(worklog);

        result.Should().BeFalse("worklog should fail for non-existent issue");
    }

    [SkippableFact]
    public async Task Should_HandleLargeJqlResults_When_ManyIssuesExist()
    {
        Skip.IfNot(_isConfigured, _skipReason);

        var issues = await _jiraService!.GetAssignedIssuesAsync();

        issues.Should().NotBeNull();
        issues.Count.Should().BeLessThanOrEqualTo(100, "service should respect maxResults=100 limit");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    private class TestJiraAuthService(string accessToken, string cloudId) : IJiraAuthService
    {
        public bool IsLoggedIn => true;
        public string? CurrentUsername => "Integration Test User";
        public string? CurrentUserEmail => null;
        public string? CurrentSiteUrl => null;
        public string? CurrentCloudId => cloudId;
        public event EventHandler<AuthStateChangedEventArgs>? AuthStateChanged;

        public Task<bool> LoginAsync() => Task.FromResult(true);
        public Task<bool> LogoutAsync() => Task.FromResult(true);
        public Task<string?> GetCurrentUserAsync() => Task.FromResult<string?>(CurrentUsername);
        public Task<bool> TryAutoLoginAsync() => Task.FromResult(true);
        public Task<string?> GetAccessTokenAsync() => Task.FromResult<string?>(accessToken);
    }
}
```

- [ ] **Step 2: Run tests to verify they compile and skip cleanly without credentials**

Run: `dotnet test tests/FocusTray.IntegrationTests --filter FullyQualifiedName~JiraServiceIntegrationTests`
Expected: All 5 tests reported as **Skipped** (no `JIRA_ACCESS_TOKEN`/`JIRA_CLOUD_ID` set in this environment) — no compile errors, no failures.

- [ ] **Step 3: Commit**

```bash
git add tests/FocusTray.IntegrationTests/Jira/JiraServiceIntegrationTests.cs
git commit -m "test(jira): rewrite JIRA integration tests for OAuth2 access tokens"
```

---

### Task 14: Rewrite `docs/JIRA_INTEGRATION.md`

**Files:**
- Modify: `docs/JIRA_INTEGRATION.md`

No test — documentation only. Self-review against the spec substitutes for a test here.

- [ ] **Step 1: Replace the Setup, Configuration Details, Security, API Endpoints, and FAQ sections**

Apply these targeted replacements (keep "Features", "Using JIRA Integration", "Advanced Usage"/JQL sections as-is — they're unaffected by the auth change):

Replace the whole `## Setup Instructions` section with:

```markdown
## Setup Instructions

### Step 1: Register a JIRA OAuth 2.0 (3LO) app (one-time, per fork/install)

FocusTray authenticates to JIRA Cloud using OAuth 2.0 (3LO) with PKCE, the same
browser-based sign-in style used for Microsoft Teams. If you're running an
official FocusTray build, this is already configured — skip to Step 2.

If you're building FocusTray from source, you (or your organization) need your
own app registration:

1. Go to the [Atlassian Developer Console](https://developer.atlassian.com/console/myapps)
2. Create a new app → **OAuth 2.0 (3LO)** integration, **public client** (no client secret)
3. Add these API scopes: `read:jira-work`, `write:jira-work`, `read:jira-user`, `offline_access`
4. Set the **Callback URL** to `http://localhost:8082/callback`
5. Copy the **Client ID** and set it as `JiraOAuthConfiguration.ClientId` in
   `src/FocusTray.Infrastructure/Jira/JiraOAuthConfiguration.cs`

### Step 2: Connect FocusTray to JIRA

1. Right-click the FocusTray system tray icon
2. Select **"JIRA Settings"** → **"Login to JIRA"**
3. Click **"Sign in with Atlassian"** — your browser opens Atlassian's login page
4. Log in and approve access
5. If your account has access to more than one JIRA site, choose which one to connect
6. FocusTray shows "Successfully signed in to JIRA as \<your name\>!"

**JQL Filter** (optional, configured separately via **"JIRA Advanced Settings"**):
- Default: `assignee = currentUser() AND statusCategory != Done`
- Customize to show specific issues, e.g.:
  - Only bugs: `assignee = currentUser() AND type = Bug AND status != Done`
  - Specific project: `project = MYPROJECT AND assignee = currentUser()`
```

Replace the `### Settings File Format` code block and the note beneath it with:

```markdown
### Settings File Format

```json
{
  "JiraConfiguration": {
    "JqlFilter": "assignee = currentUser() AND statusCategory != Done"
  }
}
```

**Note**: OAuth2 tokens (access token, refresh token, expiry, and the connected
JIRA site) are stored encrypted (Windows DPAPI) in
`%LocalAppData%\FocusTray\jira_token_cache.dat`, never in `settings.json`.
```

Replace `## Security Best Practices` with:

```markdown
## Security Best Practices

1. **Tokens are encrypted at rest** using Windows DPAPI, scoped to your Windows
   user account — the same mechanism used for Microsoft Teams tokens.
2. **Refresh tokens rotate**: JIRA issues a new refresh token on every use;
   FocusTray always persists the newest one and discards the old.
3. **Revoke access anytime** from
   [your Atlassian account's connected apps](https://id.atlassian.com/manage-profile/security) —
   FocusTray will require you to sign in again on its next JIRA request.
4. **Never commit `settings.json` or the token cache file to version control.**
```

Replace `## API Endpoints Used` with:

```markdown
## API Endpoints Used

FocusTray uses these JIRA Cloud REST API v2 endpoints, proxied through
Atlassian's OAuth 2.0 (3LO) API gateway (`https://api.atlassian.com/ex/jira/{cloudId}/...`):

1. **Connection Test**: `GET /rest/api/2/myself`
2. **Get Issues**: `GET /rest/api/2/search/jql?jql={filter}&fields=key,summary,issuetype,status&maxResults=100`
3. **Add Worklog**: `POST /rest/api/2/issue/{issueKey}/worklog`

**Authentication**: OAuth 2.0 (3LO), Authorization Code + PKCE, `Authorization: Bearer <access_token>`.
```

Replace the two Basic-Auth-specific FAQ entries:

```markdown
**Q: Can I use my Atlassian password or an API token instead of signing in with OAuth2?**
A: No. FocusTray only supports OAuth 2.0 sign-in for JIRA now — this is more
secure than API tokens and matches how Microsoft Teams already authenticates.

**Q: Does this support multiple JIRA sites?**
A: Yes — if your Atlassian account has access to more than one JIRA Cloud site,
FocusTray asks you to pick one when you sign in. To switch sites later, log out
and log back in.
```

- [ ] **Step 2: Add a "Manual OAuth2 test procedure" section**

Add this near the end of the document, before `## Support`:

```markdown
## Manual OAuth2 Test Procedure

The interactive login flow (browser + local loopback listener) can't be
automated in CI, the same way Microsoft Teams' login flow can't. To verify it
manually after changing JIRA auth code:

1. Run FocusTray from source, open **JIRA Settings → Login to JIRA**
2. Click **"Sign in with Atlassian"** — confirm the system browser opens
3. Approve access — confirm the browser tab shows a success page and FocusTray
   shows "Successfully signed in to JIRA as ..."
4. If your account has multiple sites, confirm the site picker appears and the
   chosen site is the one FocusTray actually queries
5. Close FocusTray, reopen it — confirm auto-login succeeds silently (no browser popup)
6. Use **JIRA Advanced Settings → Test Query** to confirm issues load
7. Complete a JIRA-linked focus session and confirm the worklog is created
8. Click **Logout** — confirm `%LocalAppData%\FocusTray\jira_token_cache.dat` is deleted
   and the next JIRA action prompts a fresh login

To populate `JIRA_ACCESS_TOKEN`/`JIRA_CLOUD_ID` for the automated integration
tests (`tests/FocusTray.IntegrationTests`), sign in once via the steps above,
then read the cached access token and cloud ID from
`%LocalAppData%\FocusTray\jira_token_cache.dat` (decrypt with the same DPAPI
call `JiraTokenCacheHelper.Load()` uses) and export them as environment
variables before running `dotnet test tests/FocusTray.IntegrationTests`. Access
tokens are short-lived, so re-export before each run.
```

- [ ] **Step 3: Commit**

```bash
git add docs/JIRA_INTEGRATION.md
git commit -m "docs(jira): rewrite JIRA_INTEGRATION.md for OAuth 2.0 (3LO)"
```

---

### Task 15: Full solution build + full test suite (final verification)

**Files:** none (verification only)

- [ ] **Step 1: Build the full solution**

Run: `dotnet build FocusTray.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 2: Run the full unit test suite**

Run: `dotnet test tests/FocusTray.Tests`
Expected: All tests pass, including every new `Jira*Tests` class added in Tasks 1–9 and every pre-existing test untouched by this migration (e.g. Teams tests).

- [ ] **Step 3: Run the integration test suite (expected all-skipped without real credentials)**

Run: `dotnet test tests/FocusTray.IntegrationTests`
Expected: `JiraServiceIntegrationTests` all skipped (no `JIRA_ACCESS_TOKEN`/`JIRA_CLOUD_ID` set); no failures.

- [ ] **Step 4: Grep for any remaining references to the old Basic Auth model**

Run: `grep -rn "CurrentCompany\|BasicAuthProvider" src/ tests/ --include=*.cs` (or the `Grep` tool with the same pattern)
Expected: No matches inside JIRA-related files (`Kubis1982.Atlassian.RestClient.BasicAuthProvider` usages should be gone from `JiraAuthService.cs`/`JiraService.cs`; any remaining `BasicAuthProvider` reference would only be a leftover import to clean up).

- [ ] **Step 5: Commit** (only if Step 4 required cleanup; otherwise this task has nothing to commit)

```bash
git add -A
git commit -m "chore(jira): final cleanup pass after OAuth2 migration"
```
