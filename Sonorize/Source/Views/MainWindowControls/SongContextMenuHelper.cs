using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Models; // For ThemeColors, Song
using Avalonia.Markup.Xaml.MarkupExtensions; // For RelativeSource

namespace Sonorize.Views.MainWindowControls;

public class SongContextMenuHelper
{
    private readonly ThemeColors _theme;

    public SongContextMenuHelper(ThemeColors theme)
    {
        _theme = theme;
        Debug.WriteLine($"[SongContextMenuHelper] Initialized.");
    }

    public ContextMenu CreateSongContextMenu()
    {
        var contextMenu = new ContextMenu();

        // Apply styles
        contextMenu.Styles.Add(new Style(s => s.OfType<ContextMenu>())
        {
            Setters =
            {
                new Setter(ContextMenu.BackgroundProperty, _theme.B_SlightlyLighterBackground),
                new Setter(ContextMenu.BorderBrushProperty, _theme.B_ControlBackgroundColor),
                new Setter(ContextMenu.BorderThicknessProperty, new Thickness(1)),
            }
        });
        contextMenu.Styles.Add(new Style(s => s.OfType<MenuItem>())
        {
            Setters =
            {
                new Setter(MenuItem.ForegroundProperty, _theme.B_TextColor),
                new Setter(MenuItem.BackgroundProperty, Brushes.Transparent)
            }
        });
        contextMenu.Styles.Add(new Style(s => s.OfType<MenuItem>().Class(":pointerover"))
        {
            Setters =
            {
                new Setter(MenuItem.BackgroundProperty, _theme.B_ControlBackgroundColor.Multiply(1.3))
            }
        });
        contextMenu.Styles.Add(new Style(s => s.OfType<MenuItem>().Class(":pressed"))
        {
            Setters =
            {
                new Setter(MenuItem.BackgroundProperty, _theme.B_AccentColor.Multiply(0.8))
            }
        });

        var editMetadataMenuItem = new MenuItem
        {
            Header = "Edit Metadata",
        };

        // When the menu opens, its DataContext becomes the item (Song).
        // We bind the CommandParameter to that DataContext.
        editMetadataMenuItem.Bind(MenuItem.CommandParameterProperty, new Binding("."));

        var commandBinding = new Binding
        {
            Path = "DataContext.OpenEditSongMetadataDialogCommand", // Path from Window's DataContext (MainWindowViewModel)
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(Window) // Find the parent Window - this is more robust
            }
        };
        editMetadataMenuItem.Bind(MenuItem.CommandProperty, commandBinding);

        contextMenu.Items.Add(editMetadataMenuItem);
        Debug.WriteLine($"[SongContextMenuHelper] CreateSongContextMenu called.");

        return contextMenu;
    }
}