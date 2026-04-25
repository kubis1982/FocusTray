using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FocusTray.Core.Services;

namespace FocusTray.ViewModels;

/// <summary>
/// ViewModel for TeamsLoginDialog.
/// </summary>
public partial class TeamsLoginDialogViewModel(ITeamsAuthService authService) : ObservableObject
{
    private readonly ITeamsAuthService _authService = authService ?? throw new ArgumentNullException(nameof(authService));

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isSuccess;

    [ObservableProperty]
    private bool _isLoading;

    [RelayCommand]
    private async Task LoginAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Signing in... Please complete authentication in your browser.";
            IsSuccess = false;

            var success = await _authService.LoginAsync();

            if (success)
            {
                StatusMessage = $"✓ Successfully signed in as {_authService.CurrentUsername}!";
                IsSuccess = true;
            }
            else
            {
                StatusMessage = "✗ Sign in failed. Please try again.";
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
}
