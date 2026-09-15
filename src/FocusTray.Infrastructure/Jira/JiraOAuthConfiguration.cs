namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Configuration for JIRA OAuth 2.0 (3LO) authentication.
/// ClientId is produced by a manual Atlassian Developer Console app registration
/// (public client + PKCE) — see docs/JIRA_INTEGRATION.md.
/// </summary>
public static class JiraOAuthConfiguration
{
    /// <summary>
    /// Sentinel value ClientId holds until a real Atlassian OAuth 2.0 (3LO) app is registered.
    /// </summary>
    public const string PlaceholderClientId = "REPLACE_WITH_ATLASSIAN_OAUTH_CLIENT_ID";

    public const string ClientId = "oqtnk1ajBy2bXpKioEO0rnPYte7YuvEH";

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

    public static bool IsPlaceholderClientId(string clientId) => clientId == PlaceholderClientId;

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
