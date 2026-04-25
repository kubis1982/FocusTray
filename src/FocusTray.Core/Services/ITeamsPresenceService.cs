namespace FocusTray.Core.Services;

/// <summary>
/// Service for managing Microsoft Teams presence and status messages.
/// </summary>
public interface ITeamsPresenceService
{
    /// <summary>
    /// Gets whether Teams presence integration is enabled (user logged in).
    /// </summary>
    bool IsEnabled { get; }
    
    /// <summary>
    /// Sets the user's Teams presence to DoNotDisturb with a custom status message.
    /// Typically called when a focus session starts.
    /// </summary>
    /// <param name="statusMessage">Custom status message to display.</param>
    /// <param name="expiryDateTime">When the status should automatically expire (session end time).</param>
    /// <returns>True if status was set successfully, false otherwise.</returns>
    Task<bool> SetFocusStatusAsync(string statusMessage, DateTime expiryDateTime);
    
    /// <summary>
    /// Clears the user's preferred presence and status message, returning to automatic status.
    /// Typically called when a focus session ends.
    /// </summary>
    /// <returns>True if status was cleared successfully, false otherwise.</returns>
    Task<bool> ClearStatusAsync();
    
    /// <summary>
    /// Gets the current user's presence information from Teams.
    /// </summary>
    /// <returns>Current presence availability string (e.g., "Available", "DoNotDisturb"), or null if request fails.</returns>
    Task<string?> GetCurrentPresenceAsync();
}
