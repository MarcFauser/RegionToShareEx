using System.Windows;

namespace RegionToShareEx;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    public App()
    {
        InitializeComponent();

        if (!RegionToShareEx.MainWindow.ValidateSettings())
        {
            Shutdown();
        }
    }
}