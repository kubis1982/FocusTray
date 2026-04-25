using System.Windows;
using FocusTray.ViewModels;

namespace FocusTray.Views;

public partial class TeamsLoginDialog : Window
{
    private readonly TeamsLoginDialogViewModel _viewModel;

    public TeamsLoginDialog(TeamsLoginDialogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        // Bind status message
        StatusTextBlock.SetBinding(System.Windows.Controls.TextBlock.TextProperty, 
            new System.Windows.Data.Binding(nameof(_viewModel.StatusMessage)) 
            { 
                Source = _viewModel 
            });

        // Bind status message color and UI state based on view model properties
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsSuccess))
            {
                StatusTextBlock.Foreground = _viewModel.IsSuccess 
                    ? System.Windows.Media.Brushes.Green 
                    : System.Windows.Media.Brushes.Red;
            }
            else if (e.PropertyName == nameof(_viewModel.StatusMessage) && 
                     _viewModel.StatusMessage.Contains("Signing in"))
            {
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Blue;
            }
            else if (e.PropertyName == nameof(_viewModel.IsLoading))
            {
                LoginButton.IsEnabled = !_viewModel.IsLoading;
                CancelButton.IsEnabled = !_viewModel.IsLoading;
                ProgressBar.Visibility = _viewModel.IsLoading 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        };
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoginCommand.ExecuteAsync(null);

        // If login successful, close dialog
        if (_viewModel.IsSuccess)
        {
            // Give user a moment to see the success message
            await Task.Delay(1500);
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
