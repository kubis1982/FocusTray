using System.Windows;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using FocusTray.ViewModels;

namespace FocusTray;

public partial class SessionConfigDialog : Window
{
    private readonly ITimerService _timerService;
    private readonly SessionConfigDialogViewModel _viewModel;

    public SessionConfigDialog(
        ITimerService timerService, 
        SessionConfigDialogViewModel viewModel)
    {
        InitializeComponent();
        
        _timerService = timerService;
        _viewModel = viewModel;
        
        DataContext = _viewModel;
        
        TaskDescriptionTextBox.Focus();
    }

    /// <summary>
    /// Gets the selected JIRA issue key if user chose a JIRA issue, null otherwise.
    /// </summary>
    public string? SelectedJiraIssueKey { get; private set; }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var validationError = _viewModel.ValidateInput();
        if (validationError != null)
        {
            MessageBox.Show(validationError, "FocusTray", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var taskDescription = _viewModel.GetEffectiveTaskDescription();
            var duration = TimeSpan.FromMinutes(_viewModel.DurationMinutes);
            
            _timerService.StartSession(taskDescription, duration);
            
            // Store the JIRA issue key for later worklog
            SelectedJiraIssueKey = _viewModel.GetSelectedIssueKey();
            
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start session: {ex.Message}", "FocusTray", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
