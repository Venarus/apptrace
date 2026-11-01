using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AppTrace.UI.ViewModels;

namespace AppTrace.UI.Views;

public partial class InstalledAppsView : UserControl
{
    public InstalledAppsView()
    {
        InitializeComponent();
    }

    private void OnFilterClicked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string filter && DataContext is InstalledAppsViewModel vm)
        {
            vm.SelectedFilter = filter;
        }
    }

    private void OnDataGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is InstalledAppsViewModel vm && vm.SelectedApp != null)
        {
            vm.ViewDetailsCommand.Execute(vm.SelectedApp);
        }
    }
}
