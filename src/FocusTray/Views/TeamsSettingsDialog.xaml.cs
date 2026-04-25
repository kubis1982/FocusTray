using System.Windows;
using FocusTray.Core.Services;

namespace FocusTray.Views;

public partial class TeamsSettingsDialog : Window
{
    private readonly ITeamsAuthService _authService;

    public TeamsSettingsDialog(ITeamsAuthService authService)
    {
        InitializeComponent();

        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        // Update UI based on login state
        UpdateLoginState();
    }

    private void UpdateLoginState()
    {
        if (_authService.IsLoggedIn)
        {
            LoggedInPanel.Visibility = Visibility.Visible;
            NotLoggedInPanel.Visibility = Visibility.Collapsed;
            UsernameRun.Text = _authService.CurrentUsername ?? "Unknown";
            EmailText.Text = $"Email: {_authService.CurrentUserEmail ?? "Unknown"}";
            LogoutButton.IsEnabled = true;
        }
        else
        {
            LoggedInPanel.Visibility = Visibility.Collapsed;
            NotLoggedInPanel.Visibility = Visibility.Visible;
            LogoutButton.IsEnabled = false;
        }
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        LogoutButton.IsEnabled = false;
        StatusTextBlock.Text = "Signing out...";
        StatusTextBlock.Foreground = System.Windows.Media.Brushes.Blue;

        var success = await _authService.LogoutAsync();

        if (success)
        {
            StatusTextBlock.Text = "✓ Successfully signed out.";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
            UpdateLoginState();

            // Auto-close after a moment
            await Task.Delay(1000);
            Close();
        }
        else
        {
            StatusTextBlock.Text = "✗ Failed to sign out. Please try again.";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
            LogoutButton.IsEnabled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
