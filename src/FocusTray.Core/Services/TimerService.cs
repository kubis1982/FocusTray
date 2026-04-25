using System.Timers;
using FocusTray.Core.Models;
using Timer = System.Timers.Timer;

namespace FocusTray.Core.Services;

/// <summary>
/// Implementation of timer service for managing focus sessions.
/// </summary>
public class TimerService : ITimerService, IDisposable
{
    private readonly Timer _timer;
    private FocusSession? _currentSession;
    private bool _disposed;

    public FocusSession? CurrentSession => _currentSession;
    
    public bool IsRunning => _currentSession?.State == TimerState.Running;

    public event EventHandler<TimeSpan>? Tick;
    public event EventHandler<FocusSession>? SessionCompleted;
    public event EventHandler<TimerState>? StateChanged;

    public TimerService()
    {
        _timer = new Timer(1000); // Tick every second
        _timer.Elapsed += OnTimerElapsed;
    }

    public bool StartSession(string taskDescription, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(taskDescription))
            throw new ArgumentException("Task description cannot be empty.", nameof(taskDescription));

        if (duration <= TimeSpan.Zero)
            throw new ArgumentException("Duration must be greater than zero.", nameof(duration));

        if (duration > TimeSpan.FromHours(24))
            throw new ArgumentException("Duration cannot exceed 24 hours.", nameof(duration));

        if (_currentSession?.State == TimerState.Running)
            throw new InvalidOperationException("A session is already active. Complete the current session first.");

        _currentSession = FocusSession.Create(taskDescription, duration, DateTime.UtcNow);

        _timer.Start();

        OnStateChanged(TimerState.Running);
        
        return true;
    }

    public bool CompleteSession()
    {
        if (_currentSession == null)
            return false;

        _timer.Stop();

        _currentSession.Stop(DateTime.UtcNow);

        OnStateChanged(TimerState.Completed);
        OnSessionCompleted(_currentSession);

        _currentSession = null;

        return true;
    }

    public bool ExtendSession(TimeSpan additionalTime)
    {
        if (_currentSession == null || _currentSession.State != TimerState.Running)
            return false;

        if (additionalTime <= TimeSpan.Zero)
            throw new ArgumentException("Additional time must be greater than zero.", nameof(additionalTime));

        var newDuration = _currentSession.Duration + additionalTime;
        if (newDuration > TimeSpan.FromHours(24))
            throw new ArgumentException("Total duration cannot exceed 24 hours.", nameof(additionalTime));

        _currentSession.Duration = newDuration;
        
        return true;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_currentSession == null || _currentSession.State != TimerState.Running)
            return;

        var timeRemaining = _currentSession.TimeRemaining;

        Tick?.Invoke(this, timeRemaining);

        if (timeRemaining <= TimeSpan.Zero)
        {
            CompleteSession();
        }
    }

    private void OnStateChanged(TimerState newState)
    {
        StateChanged?.Invoke(this, newState);
    }

    private void OnSessionCompleted(FocusSession session)
    {
        SessionCompleted?.Invoke(this, session);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _timer.Stop();
        _timer.Elapsed -= OnTimerElapsed;
        _timer.Dispose();
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
