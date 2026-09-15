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
