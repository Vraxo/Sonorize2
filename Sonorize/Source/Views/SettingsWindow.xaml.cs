using Avalonia.Controls;
using Avalonia.Interactivity;
using Sonorize.ViewModels;

namespace Sonorize.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void SaveAndCloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.SaveAndCloseCommand.Execute(null);
        }
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
