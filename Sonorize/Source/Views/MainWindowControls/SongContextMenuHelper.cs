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

    public ContextMenu CreateContextMenu(Song songDataContext)
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
            CommandParameter = songDataContext // The Song object itself
        };

        // Create the binding programmatically to avoid string parsing issues for AncestorType
        var commandBinding = new Binding
        {
            Path = "DataContext.OpenEditSongMetadataDialogCommand", // Path from ListBox's DataContext (MainWindowViewModel)
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
            {
                AncestorType = typeof(ListBox) // Find the parent ListBox
            }
        };
        editMetadataMenuItem.Bind(MenuItem.CommandProperty, commandBinding);

        contextMenu.Items.Add(editMetadataMenuItem);
        //Debug.WriteLine($"[SongContextMenuHelper] CreateContextMenu for song: {songDataContext.Title}. MenuItem command bound to ListBox.DataContext.OpenEditSongMetadataDialogCommand.");

        return contextMenu;
    }
}