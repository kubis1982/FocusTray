using System.Media;
using System.Windows;
using System.Windows.Threading;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using Microsoft.Extensions.DependencyInjection;

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
                _uiUpdateTimer?.Start();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting session: {ex.Message}", "FocusTray", 
                MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show($"Error viewing session: {ex.Message}", "FocusTray", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EndSession_Click(object sender, RoutedEventArgs e)
    {
        if (_timerService.IsRunning == true)
        {
            var result = MessageBox.Show("Are you sure you want to end the current focus session?", 
                "FocusTray", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _timerService.StopSession();
                UpdateTrayMenuState();
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
            UpdateTrayTooltip("FocusTray - Session completed! Click to start new session");
        });
    }

    private void TimerService_StateChanged(object? sender, TimerState state)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateTrayMenuState();
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

    private void ShowSessionCompletedNotification(FocusSession session)
    {
        var message = $"Focus session completed!\n\nTask: {session.TaskDescription}\nDuration: {FormatTime(session.Duration)}";
        var result = MessageBox.Show($"{message}\n\nWould you like to extend the session by 5 minutes?", 
            "Session Completed", MessageBoxButton.YesNo, MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            _timerService.ExtendSession(TimeSpan.FromMinutes(5));
            _uiUpdateTimer?.Start();
            UpdateTrayMenuState();
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

        TrayIcon?.Dispose();
        _notificationSound?.Dispose();

        base.OnClosed(e);
    }
}