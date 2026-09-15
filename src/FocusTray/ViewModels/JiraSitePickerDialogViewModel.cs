using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FocusTray.Core.Models;

namespace FocusTray.ViewModels;

public partial class JiraSitePickerDialogViewModel : ObservableObject
{
    public ObservableCollection<JiraAccessibleResource> Sites { get; }

    [ObservableProperty]
    private JiraAccessibleResource? _selectedSite;

    public JiraSitePickerDialogViewModel(IReadOnlyList<JiraAccessibleResource> sites)
    {
        Sites = new ObservableCollection<JiraAccessibleResource>(sites);
        SelectedSite = Sites.FirstOrDefault();
    }
}
