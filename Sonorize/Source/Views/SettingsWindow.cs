using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Data;
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Views;

public class SettingsWindow : Window
{
    private readonly ThemeColors _theme; // Current theme for styling THIS window

    public SettingsWindow(ThemeColors currentDisplayTheme)
    {
        _theme = currentDisplayTheme;

        // ... (Window properties and color definitions using _theme remain the same) ...
        Title = "Sonorize Settings"; Width = 500; Height = 450; // Increased height for theme selector
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner; Icon = null;
        Background = _theme.B_SlightlyLighterBackground;

        var mainPanel = new DockPanel { Margin = new Thickness(15) };

        // --- Header for Directories ---
        var dirHeaderBlock = new TextBlock
        {
            Text = "Music Directories",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = _theme.B_TextColor,
            Margin = new Thickness(0, 0, 0, 10)
        };
        // DockPanel.SetDock(dirHeaderBlock, Dock.Top); // We'll use a StackPanel for sections

        // --- Music Directories Section ---
        var directoriesPanel = new DockPanel();
        var dirManagementButtons = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5, Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(dirManagementButtons, Dock.Right);
        var addButton = new Button { Content = "Add", Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor, HorizontalAlignment = HorizontalAlignment.Stretch };
        addButton.Bind(Button.CommandProperty, new Binding("AddDirectoryCommand")); addButton.CommandParameter = this;
        var removeButton = new Button { Content = "Remove", Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor, HorizontalAlignment = HorizontalAlignment.Stretch };
        removeButton.Bind(Button.CommandProperty, new Binding("RemoveDirectoryCommand")); removeButton.Bind(Button.IsEnabledProperty, new Binding("CanRemoveDirectory"));
        dirManagementButtons.Children.Add(addButton); dirManagementButtons.Children.Add(removeButton);
        var directoryListBox = new ListBox { Background = _theme.B_ControlBackgroundColor, BorderThickness = new Thickness(1), BorderBrush = _theme.B_SecondaryTextColor, Foreground = _theme.B_TextColor, Height = 150 }; // Set a height
        directoryListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("MusicDirectories")); directoryListBox.Bind(ListBox.SelectedItemProperty, new Binding("SelectedDirectory", BindingMode.TwoWay));
        directoriesPanel.Children.Add(dirManagementButtons); directoriesPanel.Children.Add(directoryListBox);

        // --- Theme Selection Section ---
        var themeHeaderBlock = new TextBlock
        {
            Text = "Application Theme",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = _theme.B_TextColor,
            Margin = new Thickness(0, 15, 0, 5) // Margin top for separation
        };

        var themeComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            BorderBrush = _theme.B_SecondaryTextColor
        };
        themeComboBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("AvailableThemes"));
        themeComboBox.Bind(ComboBox.SelectedItemProperty, new Binding("SelectedThemeFile", BindingMode.TwoWay));

        var themeRestartNotice = new TextBlock
        {
            Text = "A restart is required for theme changes to take full effect.",
            FontSize = 10,
            Foreground = _theme.B_SecondaryTextColor,
            FontStyle = FontStyle.Italic,
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };

        // --- Layout using a main StackPanel for sections ---
        var sectionsStackPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5 };
        sectionsStackPanel.Children.Add(dirHeaderBlock);
        sectionsStackPanel.Children.Add(directoriesPanel);
        sectionsStackPanel.Children.Add(themeHeaderBlock);
        sectionsStackPanel.Children.Add(themeComboBox);
        sectionsStackPanel.Children.Add(themeRestartNotice);

        // --- Buttons Panel (Bottom) ---
        var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Margin = new Thickness(0, 20, 0, 0) };
        var saveButton = new Button { Content = "Save & Close", Background = _theme.B_AccentColor, Foreground = _theme.B_AccentForeground, Padding = new Thickness(15, 8), CornerRadius = new CornerRadius(3) };
        saveButton.Click += (s, e) => { if (DataContext is SettingsViewModel vm) { vm.SaveAndCloseCommand.Execute(null); } Close(); };
        var cancelButton = new Button { Content = "Cancel", Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor, Padding = new Thickness(15, 8), CornerRadius = new CornerRadius(3) };
        cancelButton.Click += (s, e) => Close();
        buttonsPanel.Children.Add(saveButton); buttonsPanel.Children.Add(cancelButton);

        // Main panel structure
        DockPanel.SetDock(buttonsPanel, Dock.Bottom);
        mainPanel.Children.Add(buttonsPanel);
        mainPanel.Children.Add(sectionsStackPanel); // This will fill the center

        Content = mainPanel;
    }
}