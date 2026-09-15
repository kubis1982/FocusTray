namespace FocusTray.Core.Models;

/// <summary>
/// One JIRA Cloud site accessible to the authenticated account
/// (from Atlassian's /oauth/token/accessible-resources endpoint).
/// </summary>
public class JiraAccessibleResource
{
    public string Id { get; set; } = string.Empty; // Atlassian cloudId
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
