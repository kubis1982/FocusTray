using System.Windows;
using System.Windows.Threading;
using FocusTray.Core.Models;
using FocusTray.Core.Services;

namespace FocusTray;

public partial class SessionStatusDialog : Window
{
    private readonly ITimerService _timerService;
    private readonly DispatcherTimer _updateTimer;

    public SessionStatusDialog(ITimerService timerService)
    {
        InitializeComponent();
        _timerService = timerService;
        
        // Initialize update timer to refresh display every second
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        
        // Subscribe to timer service events
        _timerService.SessionCompleted += TimerService_SessionCompleted;
        _timerService.StateChanged += TimerService_StateChanged;
        
        LoadSessionData();
        _updateTimer.Start();
    }

    private void LoadSessionData()
    {
        var session = _timerService.CurrentSession;
        if (session == null)
        {
            Close();
            return;
        }

        TaskDescriptionText.Text = session.TaskDescription;
        SessionDurationText.Text = FormatTime(session.Duration);
        UpdateTimeRemaining();
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdateTimeRemaining();
    }

    private void UpdateTimeRemaining()
    {
        var session = _timerService.CurrentSession;
        if (session == null)
        {
            _updateTimer.Stop();
            Close();
            return;
        }

        TimeRemainingText.Text = FormatTime(session.TimeRemaining);
        
        // Update button states based on session state
        ExtendButton.IsEnabled = _timerService.IsRunning;
        EndSessionButton.IsEnabled = _timerService.IsRunning;
    }

    private void TimerService_SessionCompleted(object? sender, FocusSession session)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _updateTimer.Stop();
            
            MessageBox.Show($"Focus session completed!\n\nTask: {session.TaskDescription}\nDuration: {FormatTime(session.Duration)}", 
                "Session Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            
            Close();
        });
    }

    private void TimerService_StateChanged(object? sender, TimerState state)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (state == TimerState.Idle || state == TimerState.Completed)
            {
                _updateTimer.Stop();
                Close();
            }
        });
    }

    private void ExtendButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Extend current session by 5 minutes?", 
            "Extend Session", MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            _timerService.ExtendSession(TimeSpan.FromMinutes(5));
            MessageBox.Show("Session extended by 5 minutes.", 
                "FocusTray", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void EndSessionButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to end the current focus session?", 
            "End Session", MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            _timerService.StopSession();
            MessageBox.Show("Focus session ended.", 
                "FocusTray", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
        _updateTimer.Stop();
        _timerService.SessionCompleted -= TimerService_SessionCompleted;
        _timerService.StateChanged -= TimerService_StateChanged;
        
        base.OnClosed(e);
    }
}