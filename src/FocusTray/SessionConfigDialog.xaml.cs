using System.Windows;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using FocusTray.ViewModels;

namespace FocusTray;

public partial class SessionConfigDialog : Window
{
    private readonly ITimerService _timerService;
    private readonly ITeamsPresenceService _teamsPresenceService;
    private readonly SessionConfigDialogViewModel _viewModel;

    public SessionConfigDialog(
        ITimerService timerService, 
        ITeamsPresenceService teamsPresenceService,
        SessionConfigDialogViewModel viewModel)
    {
        InitializeComponent();
        
        _timerService = timerService;
        _teamsPresenceService = teamsPresenceService;
        _viewModel = viewModel;
        
        DataContext = _viewModel;
        
        TaskDescriptionTextBox.Focus();
    }

    /// <summary>
    /// Gets the selected JIRA issue key if user chose a JIRA issue, null otherwise.
    /// </summary>
    public string? SelectedJiraIssueKey { get; private set; }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var validationError = _viewModel.ValidateInput();
        if (validationError != null)
        {
            MessageBox.Show(validationError, "FocusTray", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var taskDescription = _viewModel.GetEffectiveTaskDescription();
            var duration = TimeSpan.FromMinutes(_viewModel.DurationMinutes);
            
            _timerService.StartSession(taskDescription, duration);
            
            // Store the JIRA issue key for later worklog
            SelectedJiraIssueKey = _viewModel.GetSelectedIssueKey();
            
            // Set Teams status asynchronously (non-blocking)
            _ = SetTeamsStatusAsync(taskDescription, duration);
            
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start session: {ex.Message}", "FocusTray", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SetTeamsStatusAsync(string taskDescription, TimeSpan duration)
    {
        if (!_teamsPresenceService.IsEnabled)
        {
            // Teams not logged in, skip
            return;
        }

        try
        {
            var sessionEndTime = DateTime.Now.Add(duration);
            var statusMessage = BuildTeamsStatusMessage(taskDescription);
            
            var success = await _teamsPresenceService.SetFocusStatusAsync(statusMessage, sessionEndTime);
            
            if (!success)
            {
                // Show non-blocking notification
                ShowTeamsNotification("Failed to set Teams status", "Your focus session continues normally.");
            }
        }
        catch (Exception ex)
        {
            // Log error but don't interrupt session
            ShowTeamsNotification("Teams status error", ex.Message);
        }
    }

    private string BuildTeamsStatusMessage(string taskDescription)
    {
        // If JIRA issue was selected and user is logged in, use JIRA task description
        if (!string.IsNullOrWhiteSpace(SelectedJiraIssueKey))
        {
            return $"Focus session: {taskDescription} 🎯";
        }
        
        // Fallback message if no JIRA task
        if (string.IsNullOrWhiteSpace(taskDescription))
        {
            return "Focus session in progress 🎯";
        }
        
        return $"Focus session: {taskDescription} 🎯";
    }

    private void ShowTeamsNotification(string title, string message)
    {
        try
        {
            CommunityToolkit.WinUI.Notifications.ToastContentBuilder builder = new();
            builder.AddText(title);
            builder.AddText(message);
            builder.Show();
        }
        catch
        {
            // Ignore notification errors
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
