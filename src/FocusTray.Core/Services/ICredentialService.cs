namespace FocusTray.Core.Services;

/// <summary>
/// Service for managing credentials in secure storage (Windows Credential Manager).
/// </summary>
public interface ICredentialService
{
    /// <summary>
    /// Saves credentials to secure storage.
    /// </summary>
    /// <param name="target">Target identifier for the credentials (e.g., "FocusTray_Jira").</param>
    /// <param name="username">Username or email to store.</param>
    /// <param name="password">Password or API token to store.</param>
    /// <returns>True if credentials were saved successfully, false otherwise.</returns>
    bool SaveCredentials(string target, string username, string password);
    
    /// <summary>
    /// Loads credentials from secure storage.
    /// </summary>
    /// <param name="target">Target identifier for the credentials.</param>
    /// <returns>Tuple with (username, password) if credentials exist, or null if not found.</returns>
    (string Username, string Password)? LoadCredentials(string target);
    
    /// <summary>
    /// Deletes credentials from secure storage.
    /// </summary>
    /// <param name="target">Target identifier for the credentials.</param>
    /// <returns>True if credentials were deleted successfully, false otherwise.</returns>
    bool DeleteCredentials(string target);
    
    /// <summary>
    /// Checks if credentials exist in secure storage.
    /// </summary>
    /// <param name="target">Target identifier for the credentials.</param>
    /// <returns>True if credentials exist, false otherwise.</returns>
    bool HasStoredCredentials(string target);
}
