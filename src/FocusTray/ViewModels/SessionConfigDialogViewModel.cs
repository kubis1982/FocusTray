using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using System.Collections.ObjectModel;

namespace FocusTray.ViewModels;

/// <summary>
/// ViewModel for SessionConfigDialog.
/// </summary>
public partial class SessionConfigDialogViewModel : ObservableObject
{
    private readonly IJiraService _jiraService;

    [ObservableProperty]
    private string _taskDescription = string.Empty;

    [ObservableProperty]
    private int _durationMinutes = 25;

    [ObservableProperty]
    private bool _useJiraIssue;

    [ObservableProperty]
    private JiraIssue? _selectedIssue;

    [ObservableProperty]
    private ObservableCollection<JiraIssue> _jiraIssues = new();

    [ObservableProperty]
    private bool _isLoadingIssues;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public SessionConfigDialogViewModel(IJiraService jiraService)
    {
        _jiraService = jiraService ?? throw new ArgumentNullException(nameof(jiraService));
        
        // Don't auto-load issues - wait until user checks "Use JIRA issue"
    }

    /// <summary>
    /// Gets whether JIRA integration is available.
    /// </summary>
    public bool IsJiraEnabled => _jiraService.IsEnabled;

    /// <summary>
    /// Gets the effective task description (from JIRA issue or manual input).
    /// </summary>
    public string GetEffectiveTaskDescription()
    {
        if (UseJiraIssue && SelectedIssue != null)
        {
            return SelectedIssue.DisplayText;
        }

        return TaskDescription;
    }

    /// <summary>
    /// Gets the selected JIRA issue key, or null if not using JIRA.
    /// </summary>
    public string? GetSelectedIssueKey()
    {
        return UseJiraIssue ? SelectedIssue?.Key : null;
    }

    [RelayCommand]
    private async Task LoadJiraIssuesAsync()
    {
        if (!_jiraService.IsEnabled)
        {
            ErrorMessage = "JIRA integration is not configured.";
            return;
        }

        try
        {
            IsLoadingIssues = true;
            ErrorMessage = string.Empty;

            var issues = await _jiraService.GetAssignedIssuesAsync();
            
            JiraIssues.Clear();
            foreach (var issue in issues)
            {
                JiraIssues.Add(issue);
            }

            if (JiraIssues.Count == 0)
            {
                ErrorMessage = "No assigned issues found.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load JIRA issues: {ex.Message}";
        }
        finally
        {
            IsLoadingIssues = false;
        }
    }

    /// <summary>
    /// Validates the input and returns error message if invalid, or null if valid.
    /// </summary>
    public string? ValidateInput()
    {
        if (UseJiraIssue && SelectedIssue == null)
        {
            return "Please select a JIRA issue or uncheck 'Use JIRA issue'.";
        }

        var effectiveDescription = GetEffectiveTaskDescription();
        if (string.IsNullOrWhiteSpace(effectiveDescription))
        {
            return "Please enter a task description or select a JIRA issue.";
        }

        if (DurationMinutes <= 0)
        {
            return "Please enter a valid duration in minutes (greater than 0).";
        }

        if (DurationMinutes > 1440) // 24 hours
        {
            return "Duration cannot exceed 24 hours (1440 minutes).";
        }

        return null;
    }

    partial void OnUseJiraIssueChanged(bool value)
    {
        // Clear manual description when switching to JIRA mode
        if (value)
        {
            TaskDescription = string.Empty;
            
            // Load JIRA issues when user enables the checkbox
            if (_jiraService.IsEnabled && JiraIssues.Count == 0)
            {
                _ = LoadJiraIssuesAsync();
            }
        }
    }
}
