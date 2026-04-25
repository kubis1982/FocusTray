namespace FocusTray.Infrastructure.Teams;

/// <summary>
/// Configuration settings for Microsoft Teams integration.
/// </summary>
public class TeamsConfiguration
{
    /// <summary>
    /// Client ID for the Azure AD application.
    /// This is the application (client) ID from Azure App Registration.
    /// </summary>
    public const string ClientId = "52024678-94ec-47f8-ad4d-aff15de5986c";
    
    /// <summary>
    /// Tenant ID for the Azure AD tenant.
    /// "common" allows login from any organization.
    /// </summary>
    public const string TenantId = "common";
    
    /// <summary>
    /// Microsoft Graph API scopes required for presence management.
    /// </summary>
    public static readonly string[] Scopes = new[]
    {
        "User.Read",              // Read user profile
        "Presence.ReadWrite"      // Read and write user's presence information
    };
    
    /// <summary>
    /// Redirect URI for OAuth callback.
    /// Uses local HTTP listener for browser-based auth flow.
    /// </summary>
    public const string RedirectUri = "http://localhost:8080/";
}
