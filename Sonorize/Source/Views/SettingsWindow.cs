using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // Required for Style
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Converters; // For EnumToBooleanConverter
using Sonorize.Models;
using Sonorize.ViewModels;
using Sonorize.Views.SettingsWindowControls; // Added for new panel builders

namespace Sonorize.Views;

public class SettingsWindow : Window
{
    private readonly ThemeColors _theme;

    public SettingsWindow(ThemeColors currentDisplayTheme)
    {
        _theme = currentDisplayTheme;

        Title = "Sonorize Settings"; Width = 650; Height = 500; MinWidth = 600; MinHeight = 450;
        CanResize = true; WindowStartupLocation = WindowStartupLocation.CenterOwner; Icon = null;
        Background = _theme.B_SlightlyLighterBackground;

        // Style for the TextBox part of NumericUpDown controls in this window
        this.Styles.Add(new Style(s => s.OfType<NumericUpDown>().Descendant().OfType<TextBox>())
        {
            Setters =
            {
                new Setter(TextBox.ForegroundProperty, _theme.B_TextColor),
                new Setter(TextBox.BackgroundProperty, Brushes.Transparent),
                new Setter(TextBox.CaretBrushProperty, _theme.B_TextColor),
                new Setter(TextBox.BorderThicknessProperty, new Thickness(0)),
                new Setter(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                new Setter(TextBox.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
                new Setter(TextBox.PaddingProperty, new Thickness(10,0)) // Increased horizontal padding
            }
        });

        var mainGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("150, *"), // Left menu, Right content
            RowDefinitions = new RowDefinitions("*, Auto")      // Content area, Bottom buttons
        };

        // --- Left Navigation Menu ---
        var menuPanelContainer = SettingsMenuPanel.Create(_theme);
        Grid.SetColumn(menuPanelContainer, 0);
        Grid.SetRow(menuPanelContainer, 0);
        mainGrid.Children.Add(menuPanelContainer);

        // --- Right Content Area ---
        var contentArea = CreateContentAreaScrollViewer(); // This now returns a ScrollViewer
        Grid.SetColumn(contentArea, 1);
        Grid.SetRow(contentArea, 0);
        mainGrid.Children.Add(contentArea);

        // --- Bottom Buttons Panel ---
        var buttonsPanel = SettingsButtonPanel.Create(_theme, this);
        Grid.SetColumnSpan(buttonsPanel, 2); // Span across both columns
        Grid.SetColumn(buttonsPanel, 0);
        Grid.SetRow(buttonsPanel, 1);
        mainGrid.Children.Add(buttonsPanel);

        Content = mainGrid;
    }

    private ScrollViewer CreateContentAreaScrollViewer()
    {
        var contentPanel = new Panel { Margin = new Thickness(15) };

        var directoriesSettingsPanel = DirectoriesSettingsPanel.Create(_theme, this);
        directoriesSettingsPanel.Bind(Visual.IsVisibleProperty, new Binding("CurrentSettingsViewSection")
        {
            Converter = EnumToBooleanConverter.Instance,
            ConverterParameter = SettingsViewSection.Directories
        });

        var themeSettingsPanel = ThemeSettingsPanel.Create(_theme);
        themeSettingsPanel.Bind(Visual.IsVisibleProperty, new Binding("CurrentSettingsViewSection")
        {
            Converter = EnumToBooleanConverter.Instance,
            ConverterParameter = SettingsViewSection.Theme
        });

        var appearanceSettingsPanel = AppearanceSettingsPanel.Create(_theme);
        appearanceSettingsPanel.Bind(Visual.IsVisibleProperty, new Binding("CurrentSettingsViewSection")
        {
            Converter = EnumToBooleanConverter.Instance,
            ConverterParameter = SettingsViewSection.Appearance
        });

        var scrobblingSettingsPanel = ScrobblingSettingsPanel.Create(_theme);
        scrobblingSettingsPanel.Bind(Visual.IsVisibleProperty, new Binding("CurrentSettingsViewSection")
        {
            Converter = EnumToBooleanConverter.Instance,
            ConverterParameter = SettingsViewSection.Scrobbling
        });

        contentPanel.Children.Add(directoriesSettingsPanel);
        contentPanel.Children.Add(themeSettingsPanel);
        contentPanel.Children.Add(appearanceSettingsPanel);
        contentPanel.Children.Add(scrobblingSettingsPanel);

        var scrollViewer = new ScrollViewer
        {
            Content = contentPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        return scrollViewer;
    }
}