using System.Windows;
using FocusTray.Core.Services;

namespace FocusTray;

public partial class SessionConfigDialog : Window
{
    private readonly ITimerService _timerService;

    public SessionConfigDialog(ITimerService timerService)
    {
        InitializeComponent();
        _timerService = timerService;
        
        TaskDescriptionTextBox.Focus();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        var taskDescription = TaskDescriptionTextBox.Text.Trim();
        
        if (string.IsNullOrWhiteSpace(taskDescription))
        {
            MessageBox.Show("Please enter a task description.", "FocusTray", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TaskDescriptionTextBox.Focus();
            return;
        }

        if (!int.TryParse(DurationTextBox.Text, out var durationMinutes) || durationMinutes <= 0)
        {
            MessageBox.Show("Please enter a valid duration in minutes (greater than 0).", "FocusTray", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            DurationTextBox.Focus();
            DurationTextBox.SelectAll();
            return;
        }

        if (durationMinutes > 1440) // 24 hours
        {
            MessageBox.Show("Duration cannot exceed 24 hours (1440 minutes).", "FocusTray", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            DurationTextBox.Focus();
            DurationTextBox.SelectAll();
            return;
        }

        try
        {
            _timerService.StartSession(taskDescription, TimeSpan.FromMinutes(durationMinutes));
            
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
