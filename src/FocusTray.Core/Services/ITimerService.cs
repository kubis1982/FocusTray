using FocusTray.Core.Models;

namespace FocusTray.Core.Services;

/// <summary>
/// Service for managing focus session timers.
/// </summary>
public interface ITimerService
{
    /// <summary>
    /// Gets the current focus session, or null if no session is active.
    /// </summary>
    FocusSession? CurrentSession { get; }
    
    /// <summary>
    /// Gets whether a timer is currently running.
    /// </summary>
    bool IsRunning { get; }
    
    /// <summary>
    /// Event raised when the timer ticks (every second).
    /// </summary>
    event EventHandler<TimeSpan>? Tick;
    
    /// <summary>
    /// Event raised when a session completes.
    /// </summary>
    event EventHandler<FocusSession>? SessionCompleted;
    
    /// <summary>
    /// Event raised when the timer state changes.
    /// </summary>
    event EventHandler<TimerState>? StateChanged;
    
    /// <summary>
    /// Starts a new focus session with the specified parameters.
    /// </summary>
    /// <param name="taskDescription">Description of the task to focus on.</param>
    /// <param name="duration">Duration of the focus session.</param>
    /// <returns>True if the session started successfully, false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a session is already running.</exception>
    bool StartSession(string taskDescription, TimeSpan duration);
    
    /// <summary>
    /// Pauses the current session.
    /// </summary>
    /// <returns>True if paused successfully, false if no session is running.</returns>
    bool PauseSession();
    
    /// <summary>
    /// Resumes a paused session.
    /// </summary>
    /// <returns>True if resumed successfully, false if session is not paused.</returns>
    bool ResumeSession();
    
    /// <summary>
    /// Stops the current session immediately.
    /// </summary>
    /// <returns>True if stopped successfully, false if no session is active.</returns>
    bool StopSession();
    
    /// <summary>
    /// Extends the current session by the specified duration.
    /// </summary>
    /// <param name="additionalTime">Time to add to the session.</param>
    /// <returns>True if extended successfully, false if no session is running.</returns>
    bool ExtendSession(TimeSpan additionalTime);
}
