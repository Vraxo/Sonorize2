using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml; // Required for AvaloniaXamlLoader or InitializeComponent
using Avalonia.Interactivity; // Required for RoutedEventArgs
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Views
{
    public partial class SettingsWindow : Window // Add 'partial' keyword
    {
        // The ThemeColors field is not directly used in the constructor if
        // all theme bindings are handled by the DataContext (ViewModel) in XAML.
        // However, the constructor that takes ThemeColors is called from MainWindowViewModel.
        // We need one constructor for XAML previewer and one for runtime.
        private readonly ThemeColors? _theme;

        // Parameterless constructor for XAML previewer and instantiation if DataContext is set later
        public SettingsWindow()
        {
            InitializeComponent();
        }

        // Constructor used by your MainWindowViewModel
        public SettingsWindow(ThemeColors currentDisplayTheme)
        {
            _theme = currentDisplayTheme; // Store it if needed for specific logic not handled by bindings
            InitializeComponent();

            // If you were setting styles programmatically based on _theme, you'd do it here.
            // For instance, if Background wasn't bound in XAML:
            // this.Background = _theme.B_SlightlyLighterBackground;
            // However, with the XAML bindings, this is often not necessary for simple properties.
        }

        private void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.SaveAndCloseCommand.Execute(null);
            }
            Close();
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}