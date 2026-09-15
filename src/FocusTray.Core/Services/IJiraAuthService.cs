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
