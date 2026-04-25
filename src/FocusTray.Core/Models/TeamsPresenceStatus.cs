namespace FocusTray.Core.Models;

/// <summary>
/// Represents a Microsoft Teams presence status.
/// </summary>
public class TeamsPresenceStatus
{
    /// <summary>
    /// Gets or sets the availability status (e.g., "Available", "Busy", "DoNotDisturb", "Away").
    /// </summary>
    public string Availability { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the activity status (e.g., "Available", "InACall", "InAMeeting", "DoNotDisturb").
    /// </summary>
    public string Activity { get; set; } = string.Empty;
}
