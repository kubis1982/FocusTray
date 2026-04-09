namespace FocusTray.Core.Models;

/// <summary>
/// Represents a JIRA issue/task.
/// </summary>
public class JiraIssue
{
    /// <summary>
    /// Gets or sets the issue key (e.g., "PROJ-123").
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the issue summary/title.
    /// </summary>
    public string Summary { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the issue type (e.g., "Task", "Bug", "Story").
    /// </summary>
    public string IssueType { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the issue status (e.g., "In Progress", "To Do").
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets a display string combining key and summary.
    /// </summary>
    public string DisplayText => $"{Key}: {Summary}";
}
