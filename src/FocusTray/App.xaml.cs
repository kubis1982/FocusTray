using System.Windows;
using FocusTray.Core.Services;
using FocusTray.Core.Models;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;

namespace FocusTray;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private ITimerService? _timerService;
    private TaskbarIcon? _notifyIcon;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        services.AddSingleton<ITimerService, TimerService>();
        _serviceProvider = services.BuildServiceProvider();

        // Get timer service
        _timerService = _serviceProvider.GetRequiredService<ITimerService>();
        
        // Subscribe to timer events
        _timerService.StateChanged += OnTimerStateChanged;
        _timerService.Tick += OnTimerTick;
        _timerService.SessionCompleted += OnSessionCompleted;

        // Get notify icon from resources
        _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        if (_timerService != null)
        {
            _timerService.StateChanged -= OnTimerStateChanged;
            _timerService.Tick -= OnTimerTick;
            _timerService.SessionCompleted -= OnSessionCompleted;
            
            if (_timerService is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _notifyIcon?.Dispose();
        _serviceProvider?.Dispose();
    }

    private void StartSession_Click(object sender, RoutedEventArgs e)
    {
        if (_timerService == null)
            return;

        var dialog = new SessionConfigDialog(_timerService);
        dialog.ShowDialog();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Current.Shutdown();
    }

    private void OnTimerStateChanged(object? sender, TimerState state)
    {
        Dispatcher.Invoke(() =>
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.ToolTipText = state switch
                {
                    TimerState.Idle => "FocusTray - Ready",
                    TimerState.Running => "FocusTray - Focus session active",
                    TimerState.Paused => "FocusTray - Session paused",
                    TimerState.Completed => "FocusTray - Session completed",
                    _ => "FocusTray"
                };
            }
        });
    }

    private void OnTimerTick(object? sender, TimeSpan timeRemaining)
    {
        Dispatcher.Invoke(() =>
        {
            if (_notifyIcon != null && _timerService?.CurrentSession != null)
            {
                var task = _timerService.CurrentSession.TaskDescription;
                var remaining = $"{(int)timeRemaining.TotalMinutes:D2}:{timeRemaining.Seconds:D2}";
                _notifyIcon.ToolTipText = $"FocusTray - {task}\n{remaining} remaining";
            }
        });
    }

    private void OnSessionCompleted(object? sender, FocusSession session)
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show($"Focus session completed!\n\nTask: {session.TaskDescription}\nDuration: {session.Duration.TotalMinutes:F0} minutes",
                "FocusTray", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }
}

