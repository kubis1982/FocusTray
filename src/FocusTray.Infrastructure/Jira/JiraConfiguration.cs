namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Configuration settings for JIRA integration.
/// </summary>
public class JiraConfiguration
{
    /// <summary>
    /// Gets or sets whether JIRA integration is enabled.
    /// </summary>
    public bool Enabled { get; set; }
    
    /// <summary>
    /// Gets or sets the JIRA company name (e.g., "yourcompany").
    /// </summary>
    public string Company { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the user email for authentication (Atlassian Cloud).
    /// </summary>
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the API token for authentication.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the JQL query to fetch assigned issues.
    /// Default: "assignee = currentUser() AND statusCategory != Done"
    /// </summary>
    public string JqlFilter { get; set; } = "assignee = currentUser() AND statusCategory != Done";
    
    /// <summary>
    /// Gets whether the configuration is valid for making API calls.
    /// </summary>
    public bool IsValid =>
        Enabled &&
        !string.IsNullOrWhiteSpace(Company) &&
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(ApiToken);
}
