using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Models; // For ThemeColors

namespace Sonorize.Views.SettingsWindowControls;

public static class DirectoriesSettingsPanel
{
    public static StackPanel Create(ThemeColors theme, Window ownerWindow)
    {
        var panel = new StackPanel { Spacing = 10 };

        panel.Children.Add(new TextBlock
        {
            Text = "Music Directories",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = theme.B_TextColor,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var directoriesManagementPanel = new DockPanel();
        var dirManagementButtons = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5, Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(dirManagementButtons, Dock.Right);
        var addButton = new Button { Content = "Add", Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor, HorizontalAlignment = HorizontalAlignment.Stretch };
        // Updated binding path
        addButton.Bind(Button.CommandProperty, new Binding("MusicDirectoriesSettings.AddDirectoryCommand"));
        addButton.CommandParameter = ownerWindow; // Pass the owner window for the dialog

        var removeButton = new Button { Content = "Remove", Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor, HorizontalAlignment = HorizontalAlignment.Stretch };
        // Updated binding paths
        removeButton.Bind(Button.CommandProperty, new Binding("MusicDirectoriesSettings.RemoveDirectoryCommand"));
        removeButton.Bind(Button.IsEnabledProperty, new Binding("MusicDirectoriesSettings.CanRemoveDirectory"));
        dirManagementButtons.Children.Add(addButton);
        dirManagementButtons.Children.Add(removeButton);

        var directoryListBox = new ListBox
        {
            Background = theme.B_ControlBackgroundColor,
            BorderThickness = new Thickness(1),
            BorderBrush = theme.B_SecondaryTextColor,
            Foreground = theme.B_TextColor,
            Height = 150,
            MaxHeight = 200
        };
        // Updated binding paths
        directoryListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("MusicDirectoriesSettings.MusicDirectories"));
        directoryListBox.Bind(ListBox.SelectedItemProperty, new Binding("MusicDirectoriesSettings.SelectedDirectory", BindingMode.TwoWay));
        directoriesManagementPanel.Children.Add(dirManagementButtons);
        directoriesManagementPanel.Children.Add(directoryListBox);

        panel.Children.Add(directoriesManagementPanel);
        return panel;
    }
}