using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Sonorize.Models;
using Sonorize.ViewModels;
using System;
using System.Diagnostics;

namespace Sonorize.Views.MainWindowControls;

public class SongContextMenuHelper
{
    private readonly ThemeColors _theme;
    private readonly Func<LibraryViewModel?> _getFallbackLibraryVmFunc;

    public SongContextMenuHelper(ThemeColors theme, Func<LibraryViewModel?> getFallbackLibraryVmFunc)
    {
        _theme = theme;
        _getFallbackLibraryVmFunc = getFallbackLibraryVmFunc ?? throw new ArgumentNullException(nameof(getFallbackLibraryVmFunc));
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


        contextMenu.Opening += (sender, e) =>
        {
            if (sender is not ContextMenu cm) return;

            Debug.WriteLine($"[ContextMenuHelper.Opening] Event fired for song: {songDataContext.Title}. Attempting to resolve LibraryVM.");
            LibraryViewModel? resolvedLibraryVM = null;

            if (cm.PlacementTarget is Control placementTarget)
            {
                Debug.WriteLine($"[ContextMenuHelper.Opening] PlacementTarget is '{placementTarget.GetType().Name}'.");
                var listBoxItem = placementTarget.FindAncestorOfType<ListBoxItem>();
                Control? targetForVmSearch = listBoxItem ?? placementTarget;

                var listBox = targetForVmSearch.FindAncestorOfType<ListBox>();
                if (listBox != null)
                {
                    Debug.WriteLine($"[ContextMenuHelper.Opening] Found ancestor ListBox '{listBox.Name}'. Its DataContext is '{listBox.DataContext?.GetType().Name ?? "null"}'.");
                    if (listBox.DataContext is MainWindowViewModel mwvm && mwvm.Library != null)
                    {
                        resolvedLibraryVM = mwvm.Library;
                        Debug.WriteLine($"[ContextMenuHelper.Opening] Successfully resolved LibraryVM from ListBox.DataContext (MainWindowViewModel).");
                    }
                    else if (listBox.DataContext is LibraryViewModel lvm)
                    {
                        resolvedLibraryVM = lvm;
                        Debug.WriteLine($"[ContextMenuHelper.Opening] Successfully resolved LibraryVM directly from ListBox.DataContext.");
                    }
                    else
                    {
                        Debug.WriteLine($"[ContextMenuHelper.Opening] ListBox.DataContext is not MainWindowViewModel or LibraryViewModel, or Library is null.");
                    }
                }
                else
                {
                    Debug.WriteLine($"[ContextMenuHelper.Opening] Could not find ancestor ListBox from PlacementTarget or its ListBoxItem ancestor.");
                }
            }
            else
            {
                Debug.WriteLine($"[ContextMenuHelper.Opening] PlacementTarget is null. Cannot resolve LibraryVM dynamically.");
            }

            // Fallback to the explicitly provided _libraryVM if dynamic lookup failed.
            if (resolvedLibraryVM == null)
            {
                resolvedLibraryVM = _getFallbackLibraryVmFunc();
                if (resolvedLibraryVM != null)
                {
                    Debug.WriteLine($"[ContextMenuHelper.Opening] Using fallback LibraryVM for song: {songDataContext.Title}.");
                }
                else
                {
                    Debug.WriteLine($"[ContextMenuHelper.Opening] Fallback LibraryVM is also null for song: {songDataContext.Title}.");
                }
            }

            cm.Items.Clear();
            if (resolvedLibraryVM != null)
            {
                cm.DataContext = resolvedLibraryVM;

                var editMetadataMenuItem = new MenuItem
                {
                    Header = "Edit Metadata",
                    CommandParameter = songDataContext
                };
                editMetadataMenuItem.Bind(MenuItem.CommandProperty, new Binding("EditSongMetadataCommand"));
                cm.Items.Add(editMetadataMenuItem);
                Debug.WriteLine($"[ContextMenuHelper.Opening] ContextMenu DataContext set to LibraryVM. Command should bind for song: {songDataContext.Title}.");
            }
            else
            {
                Debug.WriteLine($"[ContextMenuHelper.Opening] CRITICAL: LibraryViewModel could not be resolved for song: {songDataContext.Title}. ContextMenu will be disabled or show error.");
                cm.DataContext = null;
                cm.Items.Add(new MenuItem { Header = " (Error: Menu commands unavailable) ", IsEnabled = false, Foreground = Brushes.Gray });
            }
        };
        return contextMenu;
    }
}