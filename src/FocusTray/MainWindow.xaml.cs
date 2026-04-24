using FocusTray.Core.Models;
using FocusTray.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.WinUI.Notifications;

namespace FocusTray;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ITimerService _timerService = default!;
    private readonly IJiraService _jiraService = default!;
    private readonly IJiraAuthService _authService = default!;
    private DispatcherTimer _uiUpdateTimer = default!;
    private SoundPlayer _notificationSound = default!;
    private string? _currentSessionJiraIssueKey;

    public MainWindow(ITimerService timerService, IJiraService jiraService, IJiraAuthService authService)
    {
        InitializeComponent();

        _timerService = timerService;
        _jiraService = jiraService;
        _authService = authService;

        InitializeWindow();
    }

    private void InitializeWindow()
    {
        // Initialize UI update timer
        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uiUpdateTimer.Tick += UiUpdateTimer_Tick;

        // Initialize notification sound
        _notificationSound = new SoundPlayer();

        // Subscribe to timer service events
        _timerService.Tick += TimerService_Tick;
        _timerService.SessionCompleted += TimerService_SessionCompleted;
        _timerService.StateChanged += TimerService_StateChanged;

        // Subscribe to auth state changes
        _authService.AuthStateChanged += AuthService_AuthStateChanged;

        // Update JIRA menu state on initialization
        UpdateJiraMenuState();

        // Hide window on startup
        WindowState = WindowState.Minimized;
        Hide();
    }

    private void StartSession_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sessionDialog = App.Services.GetRequiredService<SessionConfigDialog>();
            var result = sessionDialog.ShowDialog();

            if (result == true)
            {
                // Store JIRA issue key if user selected one
                _currentSessionJiraIssueKey = sessionDialog.SelectedJiraIssueKey;
                
                UpdateTrayMenuState();
                UpdateTrayIcon(true);
                _uiUpdateTimer?.Start();
            }
        }
        catch (Exception ex)
        {
            ShowErrorNotification("FocusTray Error", $"Error starting session: {ex.Message}");
        }
    }

    private void ViewSession_Click(object sender, RoutedEventArgs e)
    {
        if (_timerService.CurrentSession == null)
            return;

        try
        {
            var statusDialog = new SessionStatusDialog(_timerService);
            statusDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowErrorNotification("FocusTray Error", $"Error viewing session: {ex.Message}");
        }
    }

    private void EndSession_Click(object sender, RoutedEventArgs e)
    {
        if (_timerService.IsRunning == true)
        {
            var result = System.Windows.MessageBox.Show("Are you sure you want to end the current focus session?", 
                "FocusTray", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                var session = _timerService.CurrentSession;
                _timerService.StopSession();
                
                // Prompt for worklog if JIRA issue was selected
                if (session != null)
                {
                    PromptForWorklog(session);
                }
                
                UpdateTrayMenuState();
                UpdateTrayIcon(false);
                _uiUpdateTimer?.Stop();
                UpdateTrayTooltip("FocusTray - Click to start focus session");
            }
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void JiraLogin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var loginDialog = App.Services.GetRequiredService<Views.JiraLoginDialog>();
            loginDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowErrorNotification("FocusTray Error", $"Error opening JIRA login: {ex.Message}");
        }
    }

    private async void JiraLogout_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to logout from JIRA? Your credentials will be removed.",
            "Logout from JIRA",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _authService.LogoutAsync();
                ShowInfoNotification("JIRA Logout", "Successfully logged out from JIRA");
            }
            catch (Exception ex)
            {
                ShowErrorNotification("FocusTray Error", $"Error during logout: {ex.Message}");
            }
        }
    }

    private void JiraAdvancedSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settingsDialog = App.Services.GetRequiredService<Views.JiraAdvancedSettingsDialog>();
            settingsDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            ShowErrorNotification("FocusTray Error", $"Error opening JIRA settings: {ex.Message}");
        }
    }

    private void AuthService_AuthStateChanged(object? sender, AuthStateChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateJiraMenuState();
        });
    }

    private void UpdateJiraMenuState()
    {
        var isLoggedIn = _authService.IsLoggedIn;

        // Update visibility of submenu items based on login state
        JiraLoginSubMenuItem.Visibility = isLoggedIn ? Visibility.Collapsed : Visibility.Visible;
        JiraStatusSubMenuItem.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;
        JiraSettingsSubMenuItem.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;
        JiraLogoutSubMenuItem.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;

        // Update status text
        if (isLoggedIn && !string.IsNullOrWhiteSpace(_authService.CurrentUsername))
        {
            JiraStatusSubMenuItem.Header = $"Logged in as: {_authService.CurrentUsername}";
        }
        else
        {
            JiraStatusSubMenuItem.Header = "Logged in as: username";
        }
    }

    private void TimerService_Tick(object? sender, TimeSpan timeRemaining)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateTrayTooltip($"FocusTray - {FormatTime(timeRemaining)} remaining");
        });
    }

    private void TimerService_SessionCompleted(object? sender, FocusSession session)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _uiUpdateTimer.Stop();

            // Play notification sound
            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch
            {
                // Log warning but continue execution
            }

            // Show completion notification
            ShowSessionCompletedNotification(session);
            
            // Prompt for worklog if JIRA issue was selected
            PromptForWorklog(session);

            UpdateTrayMenuState();
            UpdateTrayIcon(false);
            UpdateTrayTooltip("FocusTray - Session completed! Click to start new session");
        });
    }

    private void TimerService_StateChanged(object? sender, TimerState state)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateTrayMenuState();
            UpdateTrayIcon(state == TimerState.Running);
        });
    }

    private void UiUpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_timerService.CurrentSession != null)
        {
            var timeRemaining = _timerService.CurrentSession.TimeRemaining;
            UpdateTrayTooltip($"FocusTray - {FormatTime(timeRemaining)} remaining");
        }
    }

    private void UpdateTrayMenuState()
    {
        var hasActiveSession = _timerService.IsRunning;

        StartSessionMenuItem.IsEnabled = !hasActiveSession;
        ViewSessionMenuItem.IsEnabled = hasActiveSession;
        EndSessionMenuItem.IsEnabled = hasActiveSession;
    }

    private void UpdateTrayTooltip(string tooltip)
    {
        TrayIcon.ToolTipText = tooltip;
    }

    private void UpdateTrayIcon(bool isActive)
    {
        var iconPath = isActive
            ? "pack://application:,,,/Resources/bell-with-slash-48.ico"
            : "pack://application:,,,/Resources/bell-48.ico";

        TrayIcon.IconSource = new System.Windows.Media.Imaging.BitmapImage(
            new System.Uri(iconPath, System.UriKind.Absolute));
    }

    private void ShowSessionCompletedNotification(FocusSession session)
    {
        // Show completion notification with toast only
        ShowSuccessNotification(
            "Session Completed!",
            $"Task: {session.TaskDescription}\nDuration: {FormatTime(session.Duration)}"
        );
    }

    private void ShowSuccessNotification(string title, string message)
    {
        new ToastContentBuilder()
            .SetToastScenario(ToastScenario.Reminder)
            .AddText(title)
            .AddText(message)
            .Show();
    }

    private void ShowInfoNotification(string title, string message)
    {
        new ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .Show();
    }

    private void ShowErrorNotification(string title, string message)
    {
        new ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .Show();
    }

    private async void PromptForWorklog(FocusSession session)
    {
        if (string.IsNullOrWhiteSpace(_currentSessionJiraIssueKey))
        {
            // No JIRA issue selected, skip worklog
            _currentSessionJiraIssueKey = null;
            return;
        }

        if (!_jiraService.IsEnabled)
        {
            // JIRA not enabled, skip
            _currentSessionJiraIssueKey = null;
            return;
        }

        try
        {
            // Round to nearest second to avoid rounding issues (e.g., 60.0 becoming 61)
            var timeSpent = (int)Math.Round(session.TimeElapsed.TotalSeconds);
            var issueKey = _currentSessionJiraIssueKey;
            
            var result = MessageBox.Show(
                $"Would you like to log {FormatTime(session.TimeElapsed)} of work to JIRA issue {issueKey}?",
                "Log Work to JIRA",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var worklog = new JiraWorklog
                {
                    IssueKey = issueKey,
                    TimeSpentSeconds = timeSpent,
                    Comment = $"Focus session: {session.TaskDescription}",
                    Started = session.StartTime
                };

                var success = await _jiraService.AddWorklogAsync(worklog);

                if (success)
                {
                    ShowSuccessNotification(
                        "Worklog Added",
                        $"Successfully logged {FormatTime(session.TimeElapsed)} to {issueKey}");
                }
                else
                {
                    ShowErrorNotification(
                        "Worklog Failed",
                        $"Failed to log work to {issueKey}. Please check your JIRA configuration.");
                }
            }
        }
        catch (Exception ex)
        {
            ShowErrorNotification(
                "Worklog Error",
                $"Error logging work: {ex.Message}");
        }
        finally
        {
            // Clear the issue key for next session
            _currentSessionJiraIssueKey = null;
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return $"{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
        }
        return $"{time.Minutes:D2}:{time.Seconds:D2}";
    }

    protected override void OnClosed(EventArgs e)
    {
        _uiUpdateTimer?.Stop();

        _timerService.Tick -= TimerService_Tick;
        _timerService.SessionCompleted -= TimerService_SessionCompleted;
        _timerService.StateChanged -= TimerService_StateChanged;
        
        _authService.AuthStateChanged -= AuthService_AuthStateChanged;

        TrayIcon?.Dispose();
        _notificationSound?.Dispose();

        base.OnClosed(e);
    }
}