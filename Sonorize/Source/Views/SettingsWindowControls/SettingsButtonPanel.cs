using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Sonorize.Models; // For ThemeColors
using Sonorize.ViewModels; // For SettingsViewModel

namespace Sonorize.Views.SettingsWindowControls;

public static class SettingsButtonPanel
{
    public static StackPanel Create(ThemeColors theme, Window ownerWindow)
    {
        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Thickness(15) // Consistent margin
        };

        var saveButton = new Button { Content = "Save & Close", Background = theme.B_AccentColor, Foreground = theme.B_AccentForeground, Padding = new Thickness(15, 8), CornerRadius = new CornerRadius(3) };
        saveButton.Click += (s, e) =>
        {
            if (ownerWindow.DataContext is SettingsViewModel vm)
            {
                vm.SaveAndCloseCommand.Execute(null);
            }
            ownerWindow.Close();
        };

        var cancelButton = new Button { Content = "Cancel", Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor, Padding = new Thickness(15, 8), CornerRadius = new CornerRadius(3) };
        cancelButton.Click += (s, e) => ownerWindow.Close();

        buttonsPanel.Children.Add(saveButton);
        buttonsPanel.Children.Add(cancelButton);
        return buttonsPanel;
    }
}