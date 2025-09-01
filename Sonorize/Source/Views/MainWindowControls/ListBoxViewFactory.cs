using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates; // Required for IDataTemplate
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Converters;
using Sonorize.Models; // For ThemeColors
using Sonorize.ViewModels; // For Binding paths (though not directly used for VM types here)
using System; // For Action

namespace Sonorize.Views.MainWindowControls;

public static class ListBoxViewFactory
{
    public static (ScrollViewer ScrollViewer, ListBox ListBox) CreateStyledListBoxScrollViewer(
        ThemeColors theme,
        SharedViewTemplates sharedViewTemplates,
        string name,
        string itemsSourcePath,
        string selectedItemPath,
        IDataTemplate initialItemTemplate,
        ITemplate<Panel> initialItemsPanelTemplate,
        Action<ListBox> storeInstanceCallback)
    {
        var listBoxInstance = new ListBox
        {
            Background = theme.B_ListBoxBackground,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10),
            Name = name
        };

        ApplyListBoxItemStyles(listBoxInstance, theme);

        listBoxInstance.Bind(ItemsControl.ItemsSourceProperty, new Binding(itemsSourcePath));
        listBoxInstance.Bind(ListBox.SelectedItemProperty, new Binding(selectedItemPath, BindingMode.TwoWay));

        listBoxInstance.ItemTemplate = initialItemTemplate;
        listBoxInstance.ItemsPanel = initialItemsPanelTemplate; // Correct direct assignment

        storeInstanceCallback(listBoxInstance);

        var scrollViewer = new ScrollViewer
        {
            Content = listBoxInstance,
            Padding = new Thickness(0, 0, 0, 5),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        return (scrollViewer, listBoxInstance);
    }

    private static void ApplyListBoxItemStyles(ListBox listBox, ThemeColors theme)
    {
        listBox.Styles.Add(new Style(s => s.Is<ListBoxItem>())
        {
            Setters = {
                new Setter(TemplatedControl.BackgroundProperty, new MultiBinding
                {
                    Converter = new AlternatingRowBackgroundConverter
                    {
                        DefaultBrush = theme.B_ListBoxBackground,
                        AlternateBrush = theme.B_ListBoxAlternateBackground
                    },
                    Bindings =
                    {
                        // Pass the Song object itself (the DataContext of the ListBoxItem).
                        new Binding("."),
                        // Bind to the EnableAlternatingRowColors boolean on the Song's own ViewOptions.
                        new Binding("ViewOptions.EnableAlternatingRowColors")
                    }
                }),
                new Setter(TextBlock.ForegroundProperty, theme.B_TextColor),
                new Setter(ListBoxItem.PaddingProperty, new Thickness(0)),
            }
        });

        listBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":pointerover").Not(xx => xx.Class(":selected")))
        { Setters = { new Setter(TemplatedControl.BackgroundProperty, theme.B_ControlBackgroundColor) } });
        listBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected"))
        {
            Setters = {
                new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor),
                new Setter(TextBlock.ForegroundProperty, theme.B_AccentForeground)
            }
        });
        listBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected").Class(":pointerover"))
        {
            Setters = {
                new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor),
                new Setter(TextBlock.ForegroundProperty, theme.B_AccentForeground)
            }
        });
    }
}