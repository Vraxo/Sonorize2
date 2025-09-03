using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Views.SettingsWindowControls;

public static class AppearanceSettingsPanel
{
    public static StackPanel Create(ThemeColors theme)
    {
        var panel = new StackPanel { Spacing = 15 };

        panel.Children.Add(new TextBlock
        {
            Text = "Appearance",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = theme.B_TextColor,
            Margin = new Thickness(0, 0, 0, 5)
        });

        panel.Children.Add(CreateLibraryColumnsSelector(theme));
        panel.Children.Add(new Separator { Background = theme.B_ControlBackgroundColor, Margin = new Thickness(0, 5) });

        panel.Children.Add(CreateGridImageTypeSelector(theme, "Artist", "AppearanceSettings.IsArtistGridSingle", "AppearanceSettings.IsArtistGridComposite"));
        panel.Children.Add(CreateGridImageTypeSelector(theme, "Album", "AppearanceSettings.IsAlbumGridSingle", "AppearanceSettings.IsAlbumGridComposite"));
        panel.Children.Add(CreateGridImageTypeSelector(theme, "Playlist", "AppearanceSettings.IsPlaylistGridSingle", "AppearanceSettings.IsPlaylistGridComposite"));

        panel.Children.Add(new Separator { Background = theme.B_ControlBackgroundColor, Margin = new Thickness(0, 5) });
        panel.Children.Add(CreatePlaybackBackgroundSelector(theme));
        panel.Children.Add(new Separator { Background = theme.B_ControlBackgroundColor, Margin = new Thickness(0, 5) });
        panel.Children.Add(CreatePlaybackLayoutSelector(theme));


        return panel;
    }

    private static StackPanel CreatePlaybackLayoutSelector(ThemeColors theme)
    {
        var sectionPanel = new StackPanel { Spacing = 8 };

        var title = new TextBlock
        {
            Text = "UI Layout",
            FontSize = 14,
            FontWeight = FontWeight.Normal,
            Foreground = theme.B_TextColor,
            Margin = new Thickness(0, 10, 0, 5)
        };

        var compactLayoutCheck = new CheckBox
        {
            Content = "Use compact layout (single line)",
            Foreground = theme.B_TextColor
        };
        compactLayoutCheck.Bind(CheckBox.IsCheckedProperty, new Binding("AppearanceSettings.UseCompactPlaybackControls", BindingMode.TwoWay));

        var showStatusBarCheck = new CheckBox
        {
            Content = "Show status bar",
            Foreground = theme.B_TextColor
        };
        showStatusBarCheck.Bind(CheckBox.IsCheckedProperty, new Binding("AppearanceSettings.ShowStatusBar", BindingMode.TwoWay));

        var checkPanel = new StackPanel { Spacing = 5, Margin = new Thickness(10, 0, 0, 0) };
        checkPanel.Children.Add(compactLayoutCheck);
        checkPanel.Children.Add(showStatusBarCheck);

        sectionPanel.Children.Add(title);
        sectionPanel.Children.Add(checkPanel);

        return sectionPanel;
    }

    private static StackPanel CreateLibraryColumnsSelector(ThemeColors theme)
    {
        var sectionPanel = new StackPanel { Spacing = 8 };

        var title = new TextBlock
        {
            Text = "Library List Columns & Sizing",
            FontSize = 14,
            FontWeight = FontWeight.Normal,
            Foreground = theme.B_TextColor,
            Margin = new Thickness(0, 0, 0, 5)
        };

        var showArtistCheck = new CheckBox
        {
            Content = "Show Artist Column",
            Foreground = theme.B_TextColor,
        };
        showArtistCheck.Bind(CheckBox.IsCheckedProperty, new Binding("AppearanceSettings.ShowArtistInLibrary", BindingMode.TwoWay));

        var showAlbumCheck = new CheckBox
        {
            Content = "Show Album Column",
            Foreground = theme.B_TextColor
        };
        showAlbumCheck.Bind(CheckBox.IsCheckedProperty, new Binding("AppearanceSettings.ShowAlbumInLibrary", BindingMode.TwoWay));

        var showDurationCheck = new CheckBox
        {
            Content = "Show Duration Column",
            Foreground = theme.B_TextColor
        };
        showDurationCheck.Bind(CheckBox.IsCheckedProperty, new Binding("AppearanceSettings.ShowDurationInLibrary", BindingMode.TwoWay));

        var showPlayCountCheck = new CheckBox
        {
            Content = "Show Play Count Column",
            Foreground = theme.B_TextColor
        };
        showPlayCountCheck.Bind(CheckBox.IsCheckedProperty, new Binding("AppearanceSettings.ShowPlayCountInLibrary", BindingMode.TwoWay));

        var showDateAddedCheck = new CheckBox
        {
            Content = "Show Date Added Column",
            Foreground = theme.B_TextColor
        };
        showDateAddedCheck.Bind(CheckBox.IsCheckedProperty, new Binding("AppearanceSettings.ShowDateAddedInLibrary", BindingMode.TwoWay));

        var alternatingRowCheck = new CheckBox
        {
            Content = "Enable alternating row colors (Detailed/Compact)",
            Foreground = theme.B_TextColor,
            Margin = new Thickness(0, 8, 0, 0)
        };
        alternatingRowCheck.Bind(CheckBox.IsCheckedProperty, new Binding("AppearanceSettings.EnableAlternatingRowColors", BindingMode.TwoWay));

        var checkPanel = new StackPanel { Spacing = 5, Margin = new Thickness(10, 0, 0, 0) };
        checkPanel.Children.Add(showArtistCheck);
        checkPanel.Children.Add(showAlbumCheck);
        checkPanel.Children.Add(showDurationCheck);
        checkPanel.Children.Add(showPlayCountCheck);
        checkPanel.Children.Add(showDateAddedCheck);
        checkPanel.Children.Add(alternatingRowCheck);

        sectionPanel.Children.Add(title);
        sectionPanel.Children.Add(checkPanel);

        var rowHeightPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(10, 10, 0, 0) };
        var rowHeightLabel = new TextBlock
        {
            Text = "Row Height:",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = theme.B_TextColor
        };
        var rowHeightUpDown = new NumericUpDown
        {
            Minimum = 20,
            Maximum = 80,
            Increment = 2,
            Width = 120,
            Background = theme.B_ControlBackgroundColor,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_SecondaryTextColor
        };
        rowHeightUpDown.Bind(NumericUpDown.ValueProperty, new Binding("AppearanceSettings.LibraryRowHeight", BindingMode.TwoWay));
        rowHeightPanel.Children.Add(rowHeightLabel);
        rowHeightPanel.Children.Add(rowHeightUpDown);

        sectionPanel.Children.Add(rowHeightPanel);

        return sectionPanel;
    }

    private static StackPanel CreateGridImageTypeSelector(ThemeColors theme, string label, string singleBindingPath, string compositeBindingPath)
    {
        var sectionPanel = new StackPanel { Spacing = 8 };

        var title = new TextBlock
        {
            Text = $"{label} Grid Image Style",
            FontSize = 14,
            FontWeight = FontWeight.Normal,
            Foreground = theme.B_TextColor,
            Margin = new Thickness(0, 10, 0, 5)
        };

        var singleRadio = new RadioButton
        {
            Content = "Single Representative Image",
            GroupName = $"{label}GridStyle",
            Foreground = theme.B_TextColor
        };
        singleRadio.Bind(RadioButton.IsCheckedProperty, new Binding(singleBindingPath, BindingMode.TwoWay));

        var compositeRadio = new RadioButton
        {
            Content = "4-Image Composite (if available)",
            GroupName = $"{label}GridStyle",
            Foreground = theme.B_TextColor
        };
        compositeRadio.Bind(RadioButton.IsCheckedProperty, new Binding(compositeBindingPath, BindingMode.TwoWay));

        var radioPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5, Margin = new Thickness(10, 0, 0, 0) };
        radioPanel.Children.Add(singleRadio);
        radioPanel.Children.Add(compositeRadio);

        sectionPanel.Children.Add(title);
        sectionPanel.Children.Add(radioPanel);

        return sectionPanel;
    }

    private static StackPanel CreatePlaybackBackgroundSelector(ThemeColors theme)
    {
        var sectionPanel = new StackPanel { Spacing = 8 };

        var title = new TextBlock
        {
            Text = "Playback Area Background",
            FontSize = 14,
            FontWeight = FontWeight.Normal,
            Foreground = theme.B_TextColor,
            Margin = new Thickness(0, 10, 0, 5)
        };

        var solidRadio = new RadioButton
        {
            Content = "Solid Color (from theme)",
            GroupName = "PlaybackBackgroundStyle",
            Foreground = theme.B_TextColor
        };
        solidRadio.Bind(RadioButton.IsCheckedProperty, new Binding("AppearanceSettings.IsPlaybackBackgroundSolid", BindingMode.TwoWay));

        var stretchRadio = new RadioButton
        {
            Content = "Stretched Album Art (of current song)",
            GroupName = "PlaybackBackgroundStyle",
            Foreground = theme.B_TextColor
        };
        stretchRadio.Bind(RadioButton.IsCheckedProperty, new Binding("AppearanceSettings.IsPlaybackBackgroundAlbumArtStretch", BindingMode.TwoWay));

        var abstractRadio = new RadioButton
        {
            Content = "Abstract Album Art (of current song)",
            GroupName = "PlaybackBackgroundStyle",
            Foreground = theme.B_TextColor
        };
        abstractRadio.Bind(RadioButton.IsCheckedProperty, new Binding("AppearanceSettings.IsPlaybackBackgroundAlbumArtAbstract", BindingMode.TwoWay));


        var radioPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5, Margin = new Thickness(10, 0, 0, 0) };
        radioPanel.Children.Add(solidRadio);
        radioPanel.Children.Add(stretchRadio);
        radioPanel.Children.Add(abstractRadio);

        sectionPanel.Children.Add(title);
        sectionPanel.Children.Add(radioPanel);

        return sectionPanel;
    }
}