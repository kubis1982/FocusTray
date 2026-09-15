using FocusTray.Core.Models;

namespace FocusTray.Core.Services;

/// <summary>
/// Prompts the user to pick a JIRA site when their account has access to more than one.
/// Implemented in the UI layer (FocusTray project) so that FocusTray.Infrastructure
/// never depends on WPF.
/// </summary>
public interface IJiraSitePickerPrompt
{
    /// <returns>The chosen site, or null if the user cancelled.</returns>
    Task<JiraAccessibleResource?> PickSiteAsync(IReadOnlyList<JiraAccessibleResource> sites);
}
