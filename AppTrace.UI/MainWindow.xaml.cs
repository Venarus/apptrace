using System.Windows;
using System.Windows.Media;
using AppTrace.Core.Interop;
using AppTrace.Core.Models;
using AppTrace.UI.ViewModels;

namespace AppTrace.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.VisualSettingsChanged += ApplyVisualSettings;
        Loaded += MainWindow_Loaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Apply Windows 11 Mica backdrop effect
        DwmNativeMethods.ApplyBackdrop(
            this,
            DwmNativeMethods.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW,
            isDarkMode: true);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshAllDataAsync();
    }

    private void ApplyVisualSettings(AppSettings settings)
    {
        Dispatcher.Invoke(() =>
        {
            try
            {
                // 1. Accent color
                if (ColorConverter.ConvertFromString(settings.AccentColorHex) is Color accentColor)
                {
                    Application.Current.Resources["AccentColor"] = accentColor;
                    Application.Current.Resources["AccentBrush"] = new SolidColorBrush(accentColor);
                }

                // 2. Opacity
                Opacity = Math.Clamp(settings.WindowOpacity, 0.70, 1.00);

                // 3. Backdrop type
                var backdropType = settings.BackdropType switch
                {
                    "Acrylic" => DwmNativeMethods.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_TRANSIENTWINDOW,
                    "None" => DwmNativeMethods.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_NONE,
                    _ => DwmNativeMethods.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW // Mica
                };

                bool isDark = settings.Theme != "Light";
                DwmNativeMethods.ApplyBackdrop(this, backdropType, isDarkMode: isDark);
            }
            catch { }
        });
    }
}
