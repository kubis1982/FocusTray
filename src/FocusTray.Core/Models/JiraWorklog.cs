namespace FocusTray.Core.Models;

/// <summary>
/// Represents a worklog entry to be added to a JIRA issue.
/// </summary>
public class JiraWorklog
{
    /// <summary>
    /// Gets or sets the issue key this worklog is for.
    /// </summary>
    public string IssueKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the time spent in seconds.
    /// </summary>
    public int TimeSpentSeconds { get; set; }
    
    /// <summary>
    /// Gets or sets the worklog comment/description.
    /// </summary>
    public string Comment { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets when the work was started.
    /// </summary>
    public DateTime Started { get; set; } = DateTime.UtcNow;
}
