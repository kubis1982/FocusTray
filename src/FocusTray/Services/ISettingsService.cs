using FocusTray.Infrastructure.Jira;

namespace FocusTray.Services;

/// <summary>
/// Provides access to persisted, non-secret application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the current JIRA configuration.
    /// </summary>
    JiraConfiguration JiraConfiguration { get; }

    /// <summary>
    /// Saves the JIRA configuration.
    /// </summary>
    void SaveJiraConfiguration(JiraConfiguration jiraConfig);
}
