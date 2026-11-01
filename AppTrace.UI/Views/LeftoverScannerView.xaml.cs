using System.Windows;
using System.Windows.Controls;
using AppTrace.UI.ViewModels;

namespace AppTrace.UI.Views;

public partial class LeftoverScannerView : UserControl
{
    public LeftoverScannerView()
    {
        InitializeComponent();
    }

    private void OnConfidenceFilterClicked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag && DataContext is LeftoverScannerViewModel vm)
        {
            vm.ConfidenceFilter = tag;
        }
    }
}
