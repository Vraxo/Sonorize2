using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Data; // For Binding
using Sonorize.ViewModels;
using Avalonia.Platform; // Required for WindowIcon if you were to create one, but here we set it to null.

namespace Sonorize.Views;

public class SettingsWindow : Window
{
    public SettingsWindow()
    {
        Title = "Sonorize Settings";
        Width = 500;
        Height = 400;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        // Ensure it's a dialog modal to its owner
        Icon = null; // Corrected: Remove default icon to feel more like a dialog

        // Define colors (can be shared from a Theme class or App resources)
        var backgroundColor = SolidColorBrush.Parse("#FF2D2D30"); // Slightly lighter than main window
        var controlBackgroundColor = SolidColorBrush.Parse("#FF3C3C3C");
        var textColor = SolidColorBrush.Parse("#FFF1F1F1");
        var accentColor = SolidColorBrush.Parse("#FF007ACC");

        this.Background = backgroundColor;

        var mainPanel = new DockPanel { Margin = new Thickness(15) };

        // --- Header ---
        var headerBlock = new TextBlock
        {
            Text = "Music Directories",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = textColor,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(headerBlock, Dock.Top);

        // --- Buttons Panel (Bottom) ---
        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Thickness(0, 15, 0, 0)
        };
        DockPanel.SetDock(buttonsPanel, Dock.Bottom);

        var saveButton = new Button
        {
            Content = "Save & Close",
            Background = accentColor,
            Foreground = Brushes.White,
            Padding = new Thickness(15, 8),
            CornerRadius = new CornerRadius(3)
        };
        saveButton.Click += (s, e) =>
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.SaveAndCloseCommand.Execute(null);
            }
            Close();
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Background = controlBackgroundColor,
            Foreground = textColor,
            Padding = new Thickness(15, 8),
            CornerRadius = new CornerRadius(3)
        };
        cancelButton.Click += (s, e) => Close();

        buttonsPanel.Children.Add(saveButton);
        buttonsPanel.Children.Add(cancelButton);

        // --- Directory List and Management (Center) ---
        var contentPanel = new DockPanel(); // Use DockPanel for Add/Remove buttons next to ListBox

        var directoryManagementButtons = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 5,
            Margin = new Thickness(0, 0, 10, 0) // Margin to the right of these buttons
        };
        DockPanel.SetDock(directoryManagementButtons, Dock.Right);

        var addButton = new Button { Content = "Add", Background = controlBackgroundColor, Foreground = textColor, HorizontalAlignment = HorizontalAlignment.Stretch };
        addButton.Bind(Button.CommandProperty, new Binding("AddDirectoryCommand"));
        addButton.CommandParameter = this; // Pass Window as owner for dialog

        var removeButton = new Button { Content = "Remove", Background = controlBackgroundColor, Foreground = textColor, HorizontalAlignment = HorizontalAlignment.Stretch };
        removeButton.Bind(Button.CommandProperty, new Binding("RemoveDirectoryCommand"));
        removeButton.Bind(Button.IsEnabledProperty, new Binding("CanRemoveDirectory"));

        directoryManagementButtons.Children.Add(addButton);
        directoryManagementButtons.Children.Add(removeButton);

        var directoryListBox = new ListBox
        {
            Background = controlBackgroundColor,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            Foreground = textColor,
        };
        directoryListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("MusicDirectories"));
        directoryListBox.Bind(ListBox.SelectedItemProperty, new Binding("SelectedDirectory", BindingMode.TwoWay));
        // The ListBox will fill the remaining space in contentPanel (center)

        contentPanel.Children.Add(directoryManagementButtons);
        contentPanel.Children.Add(directoryListBox); // This will be the center filling child


        mainPanel.Children.Add(headerBlock);
        mainPanel.Children.Add(buttonsPanel);
        mainPanel.Children.Add(contentPanel); // This fills the rest

        Content = mainPanel;
    }
}