using System.Windows;
using CorePulse.App.ViewModels;

namespace CorePulse.App;

public partial class DiscoveryWindow : Window
{
    public DiscoveryWindow(DiscoveryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += () => DialogResult = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
