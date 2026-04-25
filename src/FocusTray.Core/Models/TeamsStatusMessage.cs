namespace FocusTray.Core.Models;

/// <summary>
/// Represents a Microsoft Teams status message with expiry.
/// </summary>
public class TeamsStatusMessage
{
    /// <summary>
    /// Gets or sets the status message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets when the status message should expire.
    /// Null means the message persists indefinitely.
    /// </summary>
    public DateTime? ExpiryDateTime { get; set; }
}
