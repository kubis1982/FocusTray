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
    private readonly IJiraAuthService _authService;

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

    public SessionConfigDialogViewModel(IJiraService jiraService, IJiraAuthService authService)
    {
        _jiraService = jiraService ?? throw new ArgumentNullException(nameof(jiraService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        
        // Auto-load issues if user is logged in
        if (_authService.IsLoggedIn)
        {
            _ = LoadJiraIssuesAsync();
            UseJiraIssue = true; // Default to using JIRA if logged in
        }
    }

    /// <summary>
    /// Gets whether JIRA integration is available.
    /// </summary>
    public bool IsJiraEnabled => _authService.IsLoggedIn;

    /// <summary>
    /// Gets the effective task description (from JIRA issue or manual input).
    /// </summary>
    public string GetEffectiveTaskDescription()
    {
        // When logged in to JIRA, always use JIRA issue if selected
        if (IsJiraEnabled && SelectedIssue != null)
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
        // When logged in to JIRA, always return selected issue key
        return IsJiraEnabled ? SelectedIssue?.Key : null;
    }

    [RelayCommand]
    private async Task LoadJiraIssuesAsync()
    {
        if (!_authService.IsLoggedIn)
        {
            ErrorMessage = "Please login to JIRA first.";
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
        // When logged in to JIRA, require a selected issue
        if (IsJiraEnabled && SelectedIssue == null)
        {
            return "Please select a JIRA issue from the list.";
        }

        // When not logged in, require manual task description
        if (!IsJiraEnabled && string.IsNullOrWhiteSpace(TaskDescription))
        {
            return "Please enter a task description.";
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
}
