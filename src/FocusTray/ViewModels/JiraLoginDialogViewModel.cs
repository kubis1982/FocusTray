using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusTray.Core.Services;

namespace FocusTray.ViewModels;

/// <summary>
/// ViewModel for JiraLoginDialog.
/// </summary>
public partial class JiraLoginDialogViewModel(IJiraAuthService authService) : ObservableObject
{
    private readonly IJiraAuthService _authService = authService ?? throw new ArgumentNullException(nameof(authService));

    [ObservableProperty]
    private string _company = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _apiToken = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _isLoading;

    [RelayCommand]
    private async Task LoginAsync()
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(Company))
        {
            StatusMessage = "Please enter company name (e.g., 'mycompany' for mycompany.atlassian.net)";
            IsSuccess = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            StatusMessage = "Please enter your email address";
            IsSuccess = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(ApiToken))
        {
            StatusMessage = "Please enter your JIRA API token";
            IsSuccess = false;
            return;
        }

        try
        {
            IsLoading = true;
            StatusMessage = "Connecting to JIRA...";
            IsSuccess = false;

            var success = await _authService.LoginAsync(Company.Trim(), Email.Trim(), ApiToken.Trim());

            if (success)
            {
                StatusMessage = $"✓ Successfully logged in as {_authService.CurrentUsername}!";
                IsSuccess = true;
            }
            else
            {
                StatusMessage = "✗ Login failed. Please check your credentials and company name.";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Error: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Validates input and returns error message if invalid, or null if valid.
    /// </summary>
    public string? ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Company))
        {
            return "Company name is required";
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            return "Email is required";
        }

        if (string.IsNullOrWhiteSpace(ApiToken))
        {
            return "API token is required";
        }

        return null;
    }
}
