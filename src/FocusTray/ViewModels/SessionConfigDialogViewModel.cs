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
    private bool _isEndTimeMode;

    [ObservableProperty]
    private TimeOnly _targetEndTime = TimeOnly.FromDateTime(DateTime.Now.AddMinutes(25));

    [ObservableProperty]
    private TimeOnly? _selectedTimeSlot;

    [ObservableProperty]
    private ObservableCollection<TimeOnly> _availableTimeSlots = new();

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

        // Initialize time slots (every 15 minutes for next 12 hours)
        InitializeTimeSlots();

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

    /// <summary>
    /// Gets the effective duration in minutes based on selected mode.
    /// </summary>
    public int GetEffectiveDuration()
    {
        if (IsEndTimeMode)
        {            return CalculateMinutesUntilEndTime();
        }
        return DurationMinutes;
    }

    /// <summary>
    /// Calculates minutes from now until the target end time.
    /// </summary>
    private int CalculateMinutesUntilEndTime()
    {
        var now = DateTime.Now;
        var targetDateTime = DateTime.Today.Add(TargetEndTime.ToTimeSpan());

        // If target time is earlier than now, assume next day
        if (targetDateTime <= now)
        {
            targetDateTime = targetDateTime.AddDays(1);
        }

        var duration = targetDateTime - now;
        return (int)Math.Ceiling(duration.TotalMinutes);
    }

    partial void OnIsEndTimeModeChanging(bool value)
    {
        if (!value && IsEndTimeMode)
        {
            // BEFORE switching FROM end time mode to minutes mode, calculate minutes from selected time
            // We do this in OnChanging because in OnChanged, IsEndTimeMode is already false
            var calculatedMinutes = CalculateMinutesUntilEndTime();
            if (calculatedMinutes > 0 && calculatedMinutes <= 1440)
            {
                DurationMinutes = calculatedMinutes;
            }
        }
    }

    partial void OnIsEndTimeModeChanged(bool value)
    {
        if (value)
        {
            // When switching TO end time mode, refresh time slots and select appropriate one
            InitializeTimeSlots();
            var targetTime = DateTime.Now.AddMinutes(DurationMinutes);
            TargetEndTime = TimeOnly.FromDateTime(targetTime);

            // Try to select closest time slot
            SelectedTimeSlot = FindClosestTimeSlot(TargetEndTime);
        }
    }

    partial void OnSelectedTimeSlotChanged(TimeOnly? value)
    {
        if (value.HasValue && IsEndTimeMode)
        {
            TargetEndTime = value.Value;
        }
    }

    private void InitializeTimeSlots()
    {
        AvailableTimeSlots.Clear();

        var now = DateTime.Now;
        var currentTime = TimeOnly.FromDateTime(now);

        // Round up to next 15-minute interval
        var minutes = ((currentTime.Minute / 15) + 1) * 15;
        var startTime = new TimeOnly(currentTime.Hour, 0).AddMinutes(minutes);

        // Generate time slots for next 12 hours (every 15 minutes)
        for (int i = 0; i < 48; i++) // 12 hours * 4 slots per hour
        {
            var slotTime = startTime.AddMinutes(i * 15);
            AvailableTimeSlots.Add(slotTime);
        }

        // Select first slot by default
        if (AvailableTimeSlots.Count > 0)
        {
            SelectedTimeSlot = AvailableTimeSlots[0];
        }
    }

    private TimeOnly FindClosestTimeSlot(TimeOnly targetTime)
    {
        if (AvailableTimeSlots.Count == 0)
        {
            return targetTime;
        }

        return AvailableTimeSlots
            .OrderBy(slot => Math.Abs((slot.ToTimeSpan() - targetTime.ToTimeSpan()).TotalMinutes))
            .First();
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

        var effectiveDuration = GetEffectiveDuration();

        if (effectiveDuration <= 0)
        {
            return IsEndTimeMode 
                ? "Target end time must be in the future." 
                : "Please enter a valid duration in minutes (greater than 0).";
        }

        if (effectiveDuration > 1440) // 24 hours
        {
            return "Duration cannot exceed 24 hours (1440 minutes).";
        }

        return null;
    }
}
