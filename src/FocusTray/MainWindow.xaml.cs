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
    private DispatcherTimer _uiUpdateTimer = default!;
    private SoundPlayer _notificationSound = default!;

    public MainWindow(ITimerService timerService)
    {
        InitializeComponent();

        _timerService = timerService;

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
                _timerService.StopSession();
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

        TrayIcon?.Dispose();
        _notificationSound?.Dispose();

        base.OnClosed(e);
    }
}