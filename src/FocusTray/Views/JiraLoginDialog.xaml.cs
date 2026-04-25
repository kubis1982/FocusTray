using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using FocusTray.ViewModels;

namespace FocusTray.Views;

public partial class JiraLoginDialog : Window
{
    private readonly JiraLoginDialogViewModel _viewModel;

    public JiraLoginDialog(JiraLoginDialogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        // Bind text boxes to view model
        CompanyTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, 
            new System.Windows.Data.Binding(nameof(_viewModel.Company)) 
            { 
                Source = _viewModel, 
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged 
            });

        EmailTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, 
            new System.Windows.Data.Binding(nameof(_viewModel.Email)) 
            { 
                Source = _viewModel, 
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged 
            });

        // Bind status message
        StatusTextBlock.SetBinding(System.Windows.Controls.TextBlock.TextProperty, 
            new System.Windows.Data.Binding(nameof(_viewModel.StatusMessage)) 
            { 
                Source = _viewModel 
            });

        // Bind status message color based on success
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsSuccess))
            {
                StatusTextBlock.Foreground = _viewModel.IsSuccess 
                    ? System.Windows.Media.Brushes.Green 
                    : System.Windows.Media.Brushes.Red;
            }
            else if (e.PropertyName == nameof(_viewModel.StatusMessage) && 
                     _viewModel.StatusMessage.Contains("Connecting"))
            {
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Blue;
            }
            else if (e.PropertyName == nameof(_viewModel.IsLoading))
            {
                LoginButton.IsEnabled = !_viewModel.IsLoading;
                CancelButton.IsEnabled = !_viewModel.IsLoading;
            }
        };
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        // Update API token from PasswordBox (can't bind directly for security)
        _viewModel.ApiToken = ApiTokenPasswordBox.Password;

        await _viewModel.LoginCommand.ExecuteAsync(null);

        // If login successful, close dialog
        if (_viewModel.IsSuccess)
        {
            // Give user a moment to see the success message
            await Task.Delay(1000);
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch
        {
            // Ignore errors opening browser
        }
    }
}
