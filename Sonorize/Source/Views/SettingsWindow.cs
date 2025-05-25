using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Data;
using Sonorize.Models;
using Sonorize.ViewModels;
using Avalonia.Styling;
using Avalonia.Controls.Primitives; // Required for Style

namespace Sonorize.Views;

public class SettingsWindow : Window
{
    private readonly ThemeColors _theme;

    public SettingsWindow(ThemeColors currentDisplayTheme)
    {
        _theme = currentDisplayTheme;

        Title = "Sonorize Settings"; Width = 500; Height = 700;
        CanResize = false; WindowStartupLocation = WindowStartupLocation.CenterOwner; Icon = null;
        Background = _theme.B_SlightlyLighterBackground;

        // Style for the TextBox part of NumericUpDown controls in this window
        this.Styles.Add(new Style(s => s.OfType<NumericUpDown>().Descendant().OfType<TextBox>())
        {
            Setters =
            {
                new Setter(TextBox.ForegroundProperty, _theme.B_TextColor),
                new Setter(TextBox.BackgroundProperty, Brushes.Transparent), // Ensure NumericUpDown's background shows through
                new Setter(TextBox.CaretBrushProperty, _theme.B_TextColor), // For when editing
                new Setter(TextBox.BorderThicknessProperty, new Thickness(0)), // Remove internal border if any
                new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                new Setter(TextBox.HorizontalContentAlignmentProperty, HorizontalAlignment.Center), // Center the text
                new Setter(TextBox.PaddingProperty, new Thickness(2,0)) // Minimal padding
            }
        });


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

        // --- Music Directories Section ---
        var directoriesPanel = new DockPanel();
        var dirManagementButtons = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5, Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(dirManagementButtons, Dock.Right);
        var addButton = new Button { Content = "Add", Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor, HorizontalAlignment = HorizontalAlignment.Stretch };
        addButton.Bind(Button.CommandProperty, new Binding("AddDirectoryCommand")); addButton.CommandParameter = this;
        var removeButton = new Button { Content = "Remove", Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor, HorizontalAlignment = HorizontalAlignment.Stretch };
        removeButton.Bind(Button.CommandProperty, new Binding("RemoveDirectoryCommand")); removeButton.Bind(Button.IsEnabledProperty, new Binding("CanRemoveDirectory"));
        dirManagementButtons.Children.Add(addButton); dirManagementButtons.Children.Add(removeButton);
        var directoryListBox = new ListBox { Background = _theme.B_ControlBackgroundColor, BorderThickness = new Thickness(1), BorderBrush = _theme.B_SecondaryTextColor, Foreground = _theme.B_TextColor, Height = 120 };
        directoryListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("MusicDirectories")); directoryListBox.Bind(ListBox.SelectedItemProperty, new Binding("SelectedDirectory", BindingMode.TwoWay));
        directoriesPanel.Children.Add(dirManagementButtons); directoriesPanel.Children.Add(directoryListBox);

        // --- Theme Selection Section ---
        var themeHeaderBlock = new TextBlock
        {
            Text = "Application Theme",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = _theme.B_TextColor,
            Margin = new Thickness(0, 15, 0, 5)
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

        // --- Scrobbling Section ---
        var scrobblingHeaderBlock = new TextBlock
        {
            Text = "Last.fm Scrobbling",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = _theme.B_TextColor,
            Margin = new Thickness(0, 15, 0, 5)
        };

        var scrobblingEnableCheckBox = new CheckBox
        {
            Content = "Enable Scrobbling",
            Foreground = _theme.B_TextColor,
            Margin = new Thickness(0, 0, 0, 10)
        };
        scrobblingEnableCheckBox.Bind(ToggleButton.IsCheckedProperty, new Binding("LastfmScrobblingEnabled", BindingMode.TwoWay));

        var usernameLabel = new TextBlock { Text = "Username:", Foreground = _theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 5) };
        var usernameTextBox = new TextBox
        {
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            BorderBrush = _theme.B_SecondaryTextColor,
            Watermark = "Last.fm Username",
            Margin = new Thickness(0, 0, 0, 5)
        };
        usernameTextBox.Bind(TextBox.TextProperty, new Binding("LastfmUsername", BindingMode.TwoWay));

        var passwordLabel = new TextBlock { Text = "Password:", Foreground = _theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        var passwordTextBox = new TextBox
        {
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            BorderBrush = _theme.B_SecondaryTextColor,
            PasswordChar = '•',
            Watermark = "Last.fm Password"
        };
        passwordTextBox.Bind(TextBox.TextProperty, new Binding("LastfmPassword", BindingMode.TwoWay));


        var scrobblingGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(usernameLabel, 0); Grid.SetColumn(usernameLabel, 0);
        Grid.SetRow(usernameTextBox, 0); Grid.SetColumn(usernameTextBox, 1);
        Grid.SetRow(passwordLabel, 1); Grid.SetColumn(passwordLabel, 0);
        Grid.SetRow(passwordTextBox, 1); Grid.SetColumn(passwordTextBox, 1);
        scrobblingGrid.Children.Add(usernameLabel);
        scrobblingGrid.Children.Add(usernameTextBox);
        scrobblingGrid.Children.Add(passwordLabel);
        scrobblingGrid.Children.Add(passwordTextBox);

        // Scrobbling Criteria Settings
        var criteriaPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8, Margin = new Thickness(0, 10, 0, 5) };

        var percentagePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        var percentageLabel = new TextBlock { Text = "Scrobble if played for at least ", Foreground = _theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center };
        var percentageUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 100,
            Increment = 1,
            Width = 90,
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            BorderBrush = _theme.B_SecondaryTextColor
        };
        percentageUpDown.Bind(NumericUpDown.ValueProperty, new Binding("ScrobbleThresholdPercentage", BindingMode.TwoWay));
        var percentageUnitLabel = new TextBlock { Text = "% of duration", Foreground = _theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center };
        percentagePanel.Children.Add(percentageLabel); percentagePanel.Children.Add(percentageUpDown); percentagePanel.Children.Add(percentageUnitLabel);

        var absolutePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        var absoluteLabel = new TextBlock { Text = "OR Scrobble if played for at least ", Foreground = _theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center };
        var absoluteUpDown = new NumericUpDown
        {
            Minimum = 10,
            Maximum = 600,
            Increment = 10,
            Width = 100, // Lowered Minimum to 10
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            BorderBrush = _theme.B_SecondaryTextColor
        };
        absoluteUpDown.Bind(NumericUpDown.ValueProperty, new Binding("ScrobbleThresholdAbsoluteSeconds", BindingMode.TwoWay));
        var absoluteUnitLabel = new TextBlock { Text = "seconds", Foreground = _theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center };
        absolutePanel.Children.Add(absoluteLabel); absolutePanel.Children.Add(absoluteUpDown); absolutePanel.Children.Add(absoluteUnitLabel);

        var criteriaExplanation = new TextBlock
        {
            Text = "(Whichever threshold is met first, and track is > 30s)",
            FontSize = 10,
            Foreground = _theme.B_SecondaryTextColor,
            FontStyle = FontStyle.Italic,
            TextWrapping = TextWrapping.Wrap
        };

        criteriaPanel.Children.Add(percentagePanel);
        criteriaPanel.Children.Add(absolutePanel);
        criteriaPanel.Children.Add(criteriaExplanation);

        var scrobblingInfoNotice = new TextBlock
        {
            Text = "Requires a Last.fm account. Password stored locally (exercise caution).",
            FontSize = 10,
            Foreground = _theme.B_SecondaryTextColor,
            FontStyle = FontStyle.Italic,
            Margin = new Thickness(0, 15, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };


        // --- Layout using a main StackPanel for sections ---
        var sectionsStackPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5 };
        sectionsStackPanel.Children.Add(dirHeaderBlock);
        sectionsStackPanel.Children.Add(directoriesPanel);
        sectionsStackPanel.Children.Add(themeHeaderBlock);
        sectionsStackPanel.Children.Add(themeComboBox);
        sectionsStackPanel.Children.Add(themeRestartNotice);
        sectionsStackPanel.Children.Add(scrobblingHeaderBlock);
        sectionsStackPanel.Children.Add(scrobblingEnableCheckBox);
        sectionsStackPanel.Children.Add(scrobblingGrid);
        sectionsStackPanel.Children.Add(criteriaPanel);
        sectionsStackPanel.Children.Add(scrobblingInfoNotice);


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
        mainPanel.Children.Add(new ScrollViewer { Content = sectionsStackPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        Content = mainPanel;
    }
}