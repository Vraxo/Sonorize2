using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // Required for NumericUpDown
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media; // Required for Brushes
using Sonorize.Models; // For ThemeColors

namespace Sonorize.Views.SettingsWindowControls;

public static class ScrobblingSettingsPanel
{
    public static StackPanel Create(ThemeColors theme)
    {
        var panel = new StackPanel { Spacing = 10 };

        panel.Children.Add(new TextBlock
        {
            Text = "Last.fm Scrobbling",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = theme.B_TextColor,
            Margin = new Thickness(0, 0, 0, 5)
        });

        var scrobblingEnableCheckBox = new CheckBox
        {
            Content = "Enable Scrobbling",
            Foreground = theme.B_TextColor,
            Margin = new Thickness(0, 0, 0, 10)
        };
        scrobblingEnableCheckBox.Bind(ToggleButton.IsCheckedProperty, new Binding("LastfmSettings.LastfmScrobblingEnabled", BindingMode.TwoWay));
        panel.Children.Add(scrobblingEnableCheckBox);

        var usernameLabel = new TextBlock { Text = "Username:", Foreground = theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 5) };
        var usernameTextBox = new TextBox
        {
            Background = theme.B_ControlBackgroundColor,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_SecondaryTextColor,
            Watermark = "Last.fm Username",
            Margin = new Thickness(0, 0, 0, 5)
        };
        usernameTextBox.Bind(TextBox.TextProperty, new Binding("LastfmSettings.LastfmUsername", BindingMode.TwoWay));

        var passwordLabel = new TextBlock { Text = "Password:", Foreground = theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        var passwordTextBox = new TextBox
        {
            Background = theme.B_ControlBackgroundColor,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_SecondaryTextColor,
            PasswordChar = '•',
            Watermark = "Last.fm Password (if changing)"
        };
        passwordTextBox.Bind(TextBox.TextProperty, new Binding("LastfmSettings.LastfmPassword", BindingMode.TwoWay));

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
        panel.Children.Add(scrobblingGrid);

        var criteriaPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8, Margin = new Thickness(0, 10, 0, 5) };
        var percentagePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        var percentageLabel = new TextBlock { Text = "Scrobble if played for at least ", Foreground = theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center };
        var percentageUpDown = new NumericUpDown { Minimum = 1, Maximum = 100, Increment = 1, Width = 120, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor, BorderBrush = theme.B_SecondaryTextColor }; // Increased width
        percentageUpDown.Bind(NumericUpDown.ValueProperty, new Binding("LastfmSettings.ScrobbleThresholdPercentage", BindingMode.TwoWay));
        var percentageUnitLabel = new TextBlock { Text = "% of duration", Foreground = theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center };
        percentagePanel.Children.Add(percentageLabel); percentagePanel.Children.Add(percentageUpDown); percentagePanel.Children.Add(percentageUnitLabel);

        var absolutePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        var absoluteLabel = new TextBlock { Text = "OR Scrobble if played for at least ", Foreground = theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center };
        var absoluteUpDown = new NumericUpDown { Minimum = 10, Maximum = 600, Increment = 10, Width = 130, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor, BorderBrush = theme.B_SecondaryTextColor }; // Increased width
        absoluteUpDown.Bind(NumericUpDown.ValueProperty, new Binding("LastfmSettings.ScrobbleThresholdAbsoluteSeconds", BindingMode.TwoWay));
        var absoluteUnitLabel = new TextBlock { Text = "seconds", Foreground = theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center };
        absolutePanel.Children.Add(absoluteLabel); absolutePanel.Children.Add(absoluteUpDown); absolutePanel.Children.Add(absoluteUnitLabel);

        var criteriaExplanation = new TextBlock { Text = "(Whichever threshold is met first, and track is > 30s)", FontSize = 10, Foreground = theme.B_SecondaryTextColor, FontStyle = FontStyle.Italic, TextWrapping = TextWrapping.Wrap };
        criteriaPanel.Children.Add(percentagePanel); criteriaPanel.Children.Add(absolutePanel); criteriaPanel.Children.Add(criteriaExplanation);
        panel.Children.Add(criteriaPanel);

        panel.Children.Add(new TextBlock
        {
            Text = "Password stored locally only until a session key is obtained. Authentication happens when Sonorize starts or when settings are saved.",
            FontSize = 10,
            Foreground = theme.B_SecondaryTextColor,
            FontStyle = FontStyle.Italic,
            Margin = new Thickness(0, 15, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        return panel;
    }
}