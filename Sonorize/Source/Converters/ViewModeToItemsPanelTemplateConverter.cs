using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using System;
using System.Globalization;
using Sonorize.ViewModels;
using Avalonia;
using Avalonia.Markup.Xaml.Templates; // Assuming LibraryViewMode is in ViewModels

namespace Sonorize.Converters;

public class ViewModeToItemsPanelTemplateConverter : IValueConverter
{
    public static readonly ViewModeToItemsPanelTemplateConverter Instance = new();

    private readonly ItemsPanelTemplate _stackPanelTemplate;
    private readonly ItemsPanelTemplate _wrapPanelTemplate;

    public ViewModeToItemsPanelTemplateConverter()
    {
        // Standard StackPanel template (used for Detailed and Compact lists)
        _stackPanelTemplate = new ItemsPanelTemplate { Content = (Func<Panel>)(() => new StackPanel()) };

        // WrapPanel template (used for Grid view)
        _wrapPanelTemplate = new ItemsPanelTemplate
        {
            Content = (Func<Panel>)(() => new WrapPanel
            {
                // You might need to adjust spacing or orientation if desired
                // Orientation = Orientation.Horizontal, // Default
                // HorizontalSpacing = 10,
                // VerticalSpacing = 10
            })
        };
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LibraryViewMode viewMode && targetType == typeof(ItemsPanelTemplate))
        {
            switch (viewMode)
            {
                case LibraryViewMode.Detailed:
                case LibraryViewMode.Compact:
                    return _stackPanelTemplate;
                case LibraryViewMode.Grid:
                    return _wrapPanelTemplate;
                default:
                    return _stackPanelTemplate; // Fallback
            }
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return AvaloniaProperty.UnsetValue;
    }
}