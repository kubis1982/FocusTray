using FocusTray.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Text.Json;

namespace FocusTray.Infrastructure.Teams;

/// <summary>
/// Service for managing Microsoft Teams authentication via OAuth2.
/// Uses MSAL (Microsoft Authentication Library) for browser-based authentication flow.
/// </summary>
public class TeamsAuthService : ITeamsAuthService
{
    private const string CredentialTarget = "FocusTray_Teams";
    
    private readonly ICredentialService _credentialService;
    private readonly ILogger<TeamsAuthService> _logger;
    private readonly IPublicClientApplication _msalClient;
    
    private string? _currentUsername;
    private string? _currentUserEmail;
    private bool _isLoggedIn;
    private string? _accessToken;
    private DateTime _tokenExpiry;

    public TeamsAuthService(
        ICredentialService credentialService,
        ILogger<TeamsAuthService> logger)
    {
        _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Initialize MSAL PublicClientApplication
        _msalClient = PublicClientApplicationBuilder
            .Create(TeamsConfiguration.ClientId)
            .WithAuthority($"https://login.microsoftonline.com/{TeamsConfiguration.TenantId}")
            .WithRedirectUri(TeamsConfiguration.RedirectUri)
            .Build();
    }

    public bool IsLoggedIn => _isLoggedIn;
    
    public string? CurrentUsername => _currentUsername;
    
    public string? CurrentUserEmail => _currentUserEmail;

    public event EventHandler<TeamsAuthStateChangedEventArgs>? AuthStateChanged;

    public async Task<bool> LoginAsync()
    {
        try
        {
            _logger.LogInformation("Attempting to login to Microsoft Teams via OAuth2");

            // Perform interactive browser-based authentication
            var authResult = await _msalClient.AcquireTokenInteractive(TeamsConfiguration.Scopes)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync();

            if (authResult == null)
            {
                _logger.LogWarning("Login failed: no authentication result");
                return false;
            }

            // Extract user information
            _currentUsername = authResult.Account.Username;
            _currentUserEmail = authResult.Account.Username; // UPN is typically the email
            _accessToken = authResult.AccessToken;
            _tokenExpiry = authResult.ExpiresOn.LocalDateTime;

            // Save tokens to secure storage
            var tokenData = new StoredTokenData
            {
                AccessToken = authResult.AccessToken,
                RefreshToken = string.Empty, // MSAL handles refresh internally
                Username = _currentUsername,
                ExpiresAt = _tokenExpiry
            };

            var tokenJson = JsonSerializer.Serialize(tokenData);
            var credentialsSaved = _credentialService.SaveCredentials(
                CredentialTarget,
                _currentUsername,
                tokenJson);

            if (!credentialsSaved)
            {
                _logger.LogError("Failed to save tokens to secure storage");
                return false;
            }

            // Update state
            _isLoggedIn = true;

            _logger.LogInformation("Successfully logged in to Teams as {Username}", _currentUsername);

            // Raise event
            OnAuthStateChanged(new TeamsAuthStateChangedEventArgs(true, _currentUsername));

            return true;
        }
        catch (MsalException ex)
        {
            _logger.LogError(ex, "MSAL error during Teams login: {ErrorCode}", ex.ErrorCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Teams login");
            return false;
        }
    }

    public async Task<bool> LogoutAsync()
    {
        try
        {
            _logger.LogInformation("Logging out from Microsoft Teams");

            // Remove account from MSAL cache
            var accounts = await _msalClient.GetAccountsAsync();
            foreach (var account in accounts)
            {
                await _msalClient.RemoveAsync(account);
            }

            // Delete tokens from secure storage
            var deleted = _credentialService.DeleteCredentials(CredentialTarget);

            if (!deleted)
            {
                _logger.LogWarning("Failed to delete credentials (may not exist)");
            }

            // Clear state
            _currentUsername = null;
            _currentUserEmail = null;
            _accessToken = null;
            _isLoggedIn = false;

            _logger.LogInformation("Logged out successfully from Teams");

            // Raise event
            OnAuthStateChanged(new TeamsAuthStateChangedEventArgs(false, null));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Teams logout");
            return false;
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (!_isLoggedIn)
        {
            _logger.LogWarning("Cannot get access token: not logged in");
            return null;
        }

        try
        {
            // Check if current token is still valid (with 5 minute buffer)
            if (_accessToken != null && _tokenExpiry > DateTime.Now.AddMinutes(5))
            {
                _logger.LogDebug("Using cached access token");
                return _accessToken;
            }

            // Token expired or about to expire - refresh silently
            _logger.LogInformation("Access token expired, refreshing silently");

            var accounts = await _msalClient.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            if (account == null)
            {
                _logger.LogWarning("No account found in MSAL cache for silent refresh");
                _isLoggedIn = false;
                return null;
            }

            // Attempt silent token acquisition
            var authResult = await _msalClient.AcquireTokenSilent(TeamsConfiguration.Scopes, account)
                .ExecuteAsync();

            if (authResult == null)
            {
                _logger.LogWarning("Silent token refresh failed");
                _isLoggedIn = false;
                return null;
            }

            // Update cached token
            _accessToken = authResult.AccessToken;
            _tokenExpiry = authResult.ExpiresOn.LocalDateTime;

            _logger.LogInformation("Access token refreshed successfully");

            return _accessToken;
        }
        catch (MsalUiRequiredException ex)
        {
            _logger.LogWarning(ex, "Silent token refresh failed - user interaction required");
            _isLoggedIn = false;
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting access token");
            return null;
        }
    }

    public async Task<bool> TryAutoLoginAsync()
    {
        try
        {
            _logger.LogInformation("Attempting auto-login to Microsoft Teams");

            // Check if we have stored credentials
            if (!_credentialService.HasStoredCredentials(CredentialTarget))
            {
                _logger.LogInformation("No stored credentials found for auto-login");
                return false;
            }

            // Try to get account from MSAL cache
            var accounts = await _msalClient.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            if (account == null)
            {
                _logger.LogInformation("No account in MSAL cache, auto-login not possible");
                return false;
            }

            // Attempt silent token acquisition
            var authResult = await _msalClient.AcquireTokenSilent(TeamsConfiguration.Scopes, account)
                .ExecuteAsync();

            if (authResult == null)
            {
                _logger.LogWarning("Auto-login failed: could not acquire token silently");
                return false;
            }

            // Update state
            _currentUsername = authResult.Account.Username;
            _currentUserEmail = authResult.Account.Username;
            _accessToken = authResult.AccessToken;
            _tokenExpiry = authResult.ExpiresOn.LocalDateTime;
            _isLoggedIn = true;

            _logger.LogInformation("Auto-login successful as {Username}", _currentUsername);

            // Raise event
            OnAuthStateChanged(new TeamsAuthStateChangedEventArgs(true, _currentUsername));

            return true;
        }
        catch (MsalUiRequiredException ex)
        {
            _logger.LogInformation(ex, "Auto-login not possible - user interaction required");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto-login to Teams");
            return false;
        }
    }

    protected virtual void OnAuthStateChanged(TeamsAuthStateChangedEventArgs e)
    {
        AuthStateChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Data structure for storing OAuth tokens in credential storage.
    /// </summary>
    private class StoredTokenData
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
