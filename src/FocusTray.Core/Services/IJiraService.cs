using FocusTray.Core.Models;

namespace FocusTray.Core.Services;

/// <summary>
/// Service for interacting with JIRA (Atlassian Cloud).
/// </summary>
public interface IJiraService
{
    /// <summary>
    /// Gets whether JIRA integration is enabled and configured.
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// Tests the connection to JIRA with current configuration.
    /// </summary>
    /// <returns>True if connection is successful, false otherwise.</returns>
    Task<bool> TestConnectionAsync();
    
    /// <summary>
    /// Gets the list of JIRA issues assigned to the current user.
    /// Uses configured JQL filter (default: assignee = currentUser() AND statusCategory != Done).
    /// </summary>
    /// <returns>List of assigned issues, or empty list if request fails.</returns>
    Task<IReadOnlyList<JiraIssue>> GetAssignedIssuesAsync();
    
    /// <summary>
    /// Adds a worklog entry to the specified JIRA issue.
    /// </summary>
    /// <param name="worklog">Worklog information including issue key, time spent, and comment.</param>
    /// <returns>True if worklog was added successfully, false otherwise.</returns>
    Task<bool> AddWorklogAsync(JiraWorklog worklog);
}
