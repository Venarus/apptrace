using System.Windows;
using System.Windows.Controls;
using AppTrace.UI.ViewModels;

namespace AppTrace.UI.Views;

public partial class ActivityHistoryView : UserControl
{
    public ActivityHistoryView()
    {
        InitializeComponent();
    }

    private void OnActionFilterClicked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag && DataContext is ActivityHistoryViewModel vm)
        {
            vm.SelectedActionFilter = tag;
        }
    }
}
