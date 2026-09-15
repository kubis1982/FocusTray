using System.Windows;
using FocusTray.ViewModels;

namespace FocusTray.Views;

public partial class JiraSitePickerDialog : Window
{
    private readonly JiraSitePickerDialogViewModel _viewModel;

    public JiraSitePickerDialog(JiraSitePickerDialogViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        SitesListBox.ItemsSource = _viewModel.Sites;
        SitesListBox.SelectedItem = _viewModel.SelectedSite;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedSite = SitesListBox.SelectedItem as Core.Models.JiraAccessibleResource;
        DialogResult = _viewModel.SelectedSite != null;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
