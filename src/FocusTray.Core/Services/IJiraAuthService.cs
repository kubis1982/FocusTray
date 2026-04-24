namespace FocusTray.Core.Services;

/// <summary>
/// Service for managing JIRA authentication state.
/// </summary>
public interface IJiraAuthService
{
    /// <summary>
    /// Gets whether the user is currently logged in to JIRA.
    /// </summary>
    bool IsLoggedIn { get; }
    
    /// <summary>
    /// Gets the current logged-in username (display name from JIRA).
    /// Returns null if not logged in.
    /// </summary>
    string? CurrentUsername { get; }
    
    /// <summary>
    /// Gets the current user's email.
    /// Returns null if not logged in.
    /// </summary>
    string? CurrentUserEmail { get; }
    
    /// <summary>
    /// Gets the current company name.
    /// Returns null if not logged in.
    /// </summary>
    string? CurrentCompany { get; }
    
    /// <summary>
    /// Event raised when authentication state changes (login/logout).
    /// </summary>
    event EventHandler<AuthStateChangedEventArgs>? AuthStateChanged;
    
    /// <summary>
    /// Attempts to log in to JIRA with the provided credentials.
    /// Saves credentials to secure storage and verifies connection.
    /// </summary>
    /// <param name="company">Company name (e.g., "mycompany" for mycompany.atlassian.net).</param>
    /// <param name="email">User email address.</param>
    /// <param name="apiToken">JIRA API token.</param>
    /// <returns>True if login was successful, false otherwise.</returns>
    Task<bool> LoginAsync(string company, string email, string apiToken);
    
    /// <summary>
    /// Logs out the current user and clears stored credentials.
    /// </summary>
    /// <returns>True if logout was successful, false otherwise.</returns>
    Task<bool> LogoutAsync();
    
    /// <summary>
    /// Gets the current user information from JIRA.
    /// </summary>
    /// <returns>User display name if successful, null otherwise.</returns>
    Task<string?> GetCurrentUserAsync();
    
    /// <summary>
    /// Attempts to automatically log in using stored credentials.
    /// Called at application startup.
    /// </summary>
    /// <returns>True if auto-login was successful, false otherwise.</returns>
    Task<bool> TryAutoLoginAsync();
}

/// <summary>
/// Event arguments for authentication state changes.
/// </summary>
public class AuthStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets whether the user is logged in after the state change.
    /// </summary>
    public bool IsLoggedIn { get; }
    
    /// <summary>
    /// Gets the username after the state change (null if logged out).
    /// </summary>
    public string? Username { get; }
    
    public AuthStateChangedEventArgs(bool isLoggedIn, string? username)
    {
        IsLoggedIn = isLoggedIn;
        Username = username;
    }
}
