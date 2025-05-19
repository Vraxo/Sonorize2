using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Views; // Reverted to file-scoped namespace

public partial class SettingsWindow : Window
{
    private readonly ThemeColors? _theme;

    // Parameterless constructor (needed for XAML previewer, etc.)
    public SettingsWindow()
    {
        InitializeComponent();

#if DEBUG
        if (Design.IsDesignMode)
        {
            // Design-time logic here (if any)
        }
#endif
    }

    // Constructor with theme parameter
    public SettingsWindow(ThemeColors currentDisplayTheme) : this()
    {
        _theme = currentDisplayTheme;
        ApplyThemeStyles();
    }

    // This method must exist so that AvaloniaXamlCompiler can hook in.
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ApplyThemeStyles()
    {
        if (_theme == null)
            return;

        // Window background
        this.Background = _theme.B_SlightlyLighterBackground;

        // DirHeaderBlock
        var dirHeaderBlock = this.FindControl<TextBlock>("DirHeaderBlock");
        if (dirHeaderBlock != null)
            dirHeaderBlock.Foreground = _theme.B_TextColor;

        // AddButton
        var addButton = this.FindControl<Button>("AddButton");
        if (addButton != null)
        {
            addButton.Background = _theme.B_ControlBackgroundColor;
            addButton.Foreground = _theme.B_TextColor;
        }

        // RemoveButton
        var removeButton = this.FindControl<Button>("RemoveButton");
        if (removeButton != null)
        {
            removeButton.Background = _theme.B_ControlBackgroundColor;
            removeButton.Foreground = _theme.B_TextColor;
        }

        // DirectoryListBox
        var directoryListBox = this.FindControl<ListBox>("DirectoryListBox");
        if (directoryListBox != null)
        {
            directoryListBox.Background = _theme.B_ControlBackgroundColor;
            directoryListBox.BorderBrush = _theme.B_SecondaryTextColor;
            directoryListBox.Foreground = _theme.B_TextColor;
        }

        // ThemeHeaderBlock
        var themeHeaderBlock = this.FindControl<TextBlock>("ThemeHeaderBlock");
        if (themeHeaderBlock != null)
            themeHeaderBlock.Foreground = _theme.B_TextColor;

        // ThemeComboBox
        var themeComboBox = this.FindControl<ComboBox>("ThemeComboBox");
        if (themeComboBox != null)
        {
            themeComboBox.Background = _theme.B_ControlBackgroundColor;
            themeComboBox.Foreground = _theme.B_TextColor;
            themeComboBox.BorderBrush = _theme.B_SecondaryTextColor;
        }

        // ThemeRestartNotice
        var themeRestartNotice = this.FindControl<TextBlock>("ThemeRestartNotice");
        if (themeRestartNotice != null)
            themeRestartNotice.Foreground = _theme.B_SecondaryTextColor;

        // SaveButton
        var saveButton = this.FindControl<Button>("SaveButton");
        if (saveButton != null)
        {
            saveButton.Background = _theme.B_AccentColor;
            saveButton.Foreground = _theme.B_AccentForeground;
        }

        // CancelButton
        var cancelButton = this.FindControl<Button>("CancelButton");
        if (cancelButton != null)
        {
            cancelButton.Background = _theme.B_ControlBackgroundColor;
            cancelButton.Foreground = _theme.B_TextColor;
        }
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