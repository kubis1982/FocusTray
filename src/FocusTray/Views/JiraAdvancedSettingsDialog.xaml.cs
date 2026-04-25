using System.Windows;
using FocusTray.Core.Services;
using FocusTray.Services;

namespace FocusTray.Views;

public partial class JiraAdvancedSettingsDialog : Window
{
    private readonly SettingsService _settingsService;
    private readonly IJiraService _jiraService;

    public JiraAdvancedSettingsDialog(SettingsService settingsService, IJiraService jiraService)
    {
        InitializeComponent();

        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _jiraService = jiraService ?? throw new ArgumentNullException(nameof(jiraService));

        LoadSettings();
    }

    private void LoadSettings()
    {
        var config = _settingsService.JiraConfiguration;
        JqlFilterTextBox.Text = config.JqlFilter;

        // Set default if empty
        if (string.IsNullOrWhiteSpace(JqlFilterTextBox.Text))
        {
            JqlFilterTextBox.Text = "assignee = currentUser() AND statusCategory != Done";
        }
    }

    private async void TestQuery_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "Testing query...";
        StatusTextBlock.Foreground = System.Windows.Media.Brushes.Blue;

        var jqlQuery = JqlFilterTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(jqlQuery))
        {
            StatusTextBlock.Text = "Please enter a JQL query.";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
            return;
        }

        try
        {
            // Temporarily save the query to test it
            var config = _settingsService.JiraConfiguration;
            var originalJql = config.JqlFilter;
            config.JqlFilter = jqlQuery;
            _settingsService.SaveJiraConfiguration(config);

            var issues = await _jiraService.GetAssignedIssuesAsync();

            // Restore original if test fails
            if (issues.Count == 0)
            {
                StatusTextBlock.Text = "✓ Query executed successfully, but returned 0 issues.";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Orange;
            }
            else
            {
                StatusTextBlock.Text = $"✓ Query successful! Found {issues.Count} issue(s).";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Green;
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"✗ Query failed: {ex.Message}";
            StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var jqlFilter = JqlFilterTextBox.Text.Trim();

        // Use default if empty
        if (string.IsNullOrWhiteSpace(jqlFilter))
        {
            jqlFilter = "assignee = currentUser() AND statusCategory != Done";
        }

        try
        {
            var config = _settingsService.JiraConfiguration;
            config.JqlFilter = jqlFilter;
            _settingsService.SaveJiraConfiguration(config);

            MessageBox.Show(
                "JIRA settings saved successfully.",
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
