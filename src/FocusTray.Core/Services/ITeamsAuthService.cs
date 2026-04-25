namespace FocusTray.Core.Services;

/// <summary>
/// Service for managing Microsoft Teams authentication state via OAuth2.
/// </summary>
public interface ITeamsAuthService
{
    /// <summary>
    /// Gets whether the user is currently logged in to Microsoft Teams.
    /// </summary>
    bool IsLoggedIn { get; }
    
    /// <summary>
    /// Gets the current logged-in user's display name.
    /// Returns null if not logged in.
    /// </summary>
    string? CurrentUsername { get; }
    
    /// <summary>
    /// Gets the current user's email (UPN - User Principal Name).
    /// Returns null if not logged in.
    /// </summary>
    string? CurrentUserEmail { get; }
    
    /// <summary>
    /// Event raised when authentication state changes (login/logout).
    /// </summary>
    event EventHandler<TeamsAuthStateChangedEventArgs>? AuthStateChanged;
    
    /// <summary>
    /// Attempts to log in to Microsoft Teams using OAuth2 browser flow.
    /// Opens system browser for user authentication, handles callback.
    /// Saves tokens to secure storage.
    /// </summary>
    /// <returns>True if login was successful, false otherwise.</returns>
    Task<bool> LoginAsync();
    
    /// <summary>
    /// Logs out the current user and clears stored tokens.
    /// </summary>
    /// <returns>True if logout was successful, false otherwise.</returns>
    Task<bool> LogoutAsync();
    
    /// <summary>
    /// Gets the current access token for Microsoft Graph API calls.
    /// Automatically refreshes token if expired.
    /// </summary>
    /// <returns>Valid access token if logged in, null otherwise.</returns>
    Task<string?> GetAccessTokenAsync();
    
    /// <summary>
    /// Attempts to automatically log in using stored refresh token.
    /// Called at application startup.
    /// </summary>
    /// <returns>True if auto-login was successful, false otherwise.</returns>
    Task<bool> TryAutoLoginAsync();
}

/// <summary>
/// Event arguments for Teams authentication state changes.
/// </summary>
public class TeamsAuthStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets whether the user is logged in after the state change.
    /// </summary>
    public bool IsLoggedIn { get; }
    
    /// <summary>
    /// Gets the username after the state change (null if logged out).
    /// </summary>
    public string? Username { get; }
    
    public TeamsAuthStateChangedEventArgs(bool isLoggedIn, string? username)
    {
        IsLoggedIn = isLoggedIn;
        Username = username;
    }
}
