namespace FocusTray.Core.Models;

/// <summary>
/// Represents a focus work session with timer information.
/// </summary>
public class FocusSession
{
    /// <summary>
    /// Gets or sets the description of the task being worked on.
    /// </summary>
    public string TaskDescription { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the planned duration of the focus session.
    /// </summary>
    public TimeSpan Duration { get; set; }
    
    /// <summary>
    /// Gets or sets the time when the session was started.
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// Gets or sets the current state of the timer.
    /// </summary>
    public TimerState State { get; set; } = TimerState.Idle;
    
    /// <summary>
    /// Gets the time remaining in the session.
    /// </summary>
    public TimeSpan TimeRemaining
    {
        get
        {
            if (State == TimerState.Idle || State == TimerState.Completed)
                return TimeSpan.Zero;
            
            var elapsed = DateTime.UtcNow - StartTime;
            var remaining = Duration - elapsed;
            
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
    
    /// <summary>
    /// Gets the time elapsed since the session started.
    /// </summary>
    public TimeSpan TimeElapsed
    {
        get
        {
            if (State == TimerState.Idle)
                return TimeSpan.Zero;
            
            return DateTime.UtcNow - StartTime;
        }
    }
    
    /// <summary>
    /// Gets whether the session has expired.
    /// </summary>
    public bool IsExpired => TimeRemaining == TimeSpan.Zero && State == TimerState.Running;
}
