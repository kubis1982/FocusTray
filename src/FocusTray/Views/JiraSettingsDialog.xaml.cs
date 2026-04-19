using System.Windows;
using FocusTray.Infrastructure.Jira;
using FocusTray.Services;

namespace FocusTray.Views;

public partial class JiraSettingsDialog : Window
{
    private readonly SettingsService _settingsService;
    private JiraConfiguration _configuration = default!;

    public JiraSettingsDialog(SettingsService settingsService)
    {
        InitializeComponent();

        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        LoadSettings();
    }

    private void LoadSettings()
    {
        _configuration = _settingsService.JiraConfiguration;

        EnabledCheckBox.IsChecked = _configuration.Enabled;
        CompanyTextBox.Text = _configuration.Company;
        EmailTextBox.Text = _configuration.Email;
        JqlFilterTextBox.Text = _configuration.JqlFilter;

        // Note: We don't load the API token for security reasons
        // User must re-enter it if they want to change it
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "Testing connection...";
        StatusTextBlock.Foreground = System.Windows.Media.Brushes.Blue;

        // Create temporary config with current values
        var testConfig = new JiraConfiguration
        {
            Enabled = true,
            Company = CompanyTextBox.Text.Trim(),
            Email = EmailTextBox.Text.Trim(),
            ApiToken = ApiTokenPasswordBox.Password.Trim(),
            JqlFilter = JqlFilterTextBox.Text.Trim()
        };

        if (!testConfig.IsValid)
        {
            StatusTextBlock.Text = "Please fill in all required fields (Base URL, Email, API Token).";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
            return;
        }

        try
        {
            // Create temporary service with test config
            using var httpClient = new System.Net.Http.HttpClient();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<JiraService>.Instance;
            var testService = new JiraService(httpClient, testConfig, logger);

            var success = await testService.TestConnectionAsync();

            if (success)
            {
                StatusTextBlock.Text = "✓ Connection successful!";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                StatusTextBlock.Text = "✗ Connection failed. Please check your credentials and URL.";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"✗ Error: {ex.Message}";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var enabled = EnabledCheckBox.IsChecked == true;
        var company = CompanyTextBox.Text.Trim();
        var email = EmailTextBox.Text.Trim();
        var apiToken = ApiTokenPasswordBox.Password.Trim();
        var jqlFilter = JqlFilterTextBox.Text.Trim();

        // Use default JQL if empty
        if (string.IsNullOrWhiteSpace(jqlFilter))
        {
            jqlFilter = "assignee = currentUser() AND statusCategory != Done";
        }

        // Update configuration
        _configuration.Enabled = enabled;
        _configuration.Company = company;
        _configuration.Email = email;
        _configuration.JqlFilter = jqlFilter;

        // Only update API token if user entered a new one
        if (!string.IsNullOrWhiteSpace(apiToken))
        {
            _configuration.ApiToken = apiToken;
        }

        // Validate if enabled
        if (enabled && !_configuration.IsValid)
        {
            MessageBox.Show(
                "Please fill in all required fields (Base URL, Email, API Token) when enabling JIRA integration.",
                "Invalid Configuration",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            _settingsService.SaveJiraConfiguration(_configuration);

            MessageBox.Show(
                "JIRA settings saved successfully.\n\nNote: You may need to restart the application for changes to take full effect.",
                "Settings Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to save settings: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
