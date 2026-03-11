namespace FocusTray.Core.Models;

/// <summary>
/// Represents the state of the focus timer.
/// </summary>
public enum TimerState
{
    /// <summary>
    /// Timer is not running and no active session exists.
    /// </summary>
    Idle,
    
    /// <summary>
    /// Timer is actively counting down.
    /// </summary>
    Running,
    
    /// <summary>
    /// Timer is paused but session is still active.
    /// </summary>
    Paused,
    
    /// <summary>
    /// Timer has completed countdown and session has ended.
    /// </summary>
    Completed
}
