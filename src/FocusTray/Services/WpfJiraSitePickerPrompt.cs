using System.Windows;
using FocusTray.Core.Models;
using FocusTray.Core.Services;
using FocusTray.ViewModels;
using FocusTray.Views;

namespace FocusTray.Services;

/// <summary>
/// WPF implementation of the JIRA site picker prompt, shown when an account
/// has access to more than one JIRA Cloud site.
/// </summary>
public class WpfJiraSitePickerPrompt : IJiraSitePickerPrompt
{
    public Task<JiraAccessibleResource?> PickSiteAsync(IReadOnlyList<JiraAccessibleResource> sites)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var viewModel = new JiraSitePickerDialogViewModel(sites);
            var dialog = new JiraSitePickerDialog(viewModel);
            var result = dialog.ShowDialog();
            return result == true ? viewModel.SelectedSite : null;
        }).Task;
    }
}
