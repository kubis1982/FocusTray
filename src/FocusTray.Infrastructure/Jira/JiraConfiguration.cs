namespace FocusTray.Infrastructure.Jira;

/// <summary>
/// Configuration settings for JIRA integration.
/// </summary>
public class JiraConfiguration
{
    /// <summary>
    /// Gets or sets the JIRA company name (e.g., "yourcompany" for yourcompany.atlassian.net).
    /// </summary>
    public string Company { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the JQL query to fetch assigned issues.
    /// Default: "assignee = currentUser() AND statusCategory != Done"
    /// </summary>
    public string JqlFilter { get; set; } = "assignee = currentUser() AND statusCategory != Done";
}
