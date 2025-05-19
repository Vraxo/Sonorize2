using Avalonia;
using Avalonia.Controls;
using Avalonia.Data; // Required for BindingMode

namespace Sonorize.Controls;

public partial class SearchBarControl : UserControl
{
    // Define a bindable property for the search query text
    public static readonly StyledProperty<string> QueryTextProperty =
        AvaloniaProperty.Register<SearchBarControl, string>(
            nameof(QueryText),
            defaultValue: string.Empty,
            defaultBindingMode: BindingMode.TwoWay); // Ensure TwoWay binding

    public string QueryText
    {
        get => GetValue(QueryTextProperty);
        set => SetValue(QueryTextProperty, value);
    }

    public SearchBarControl()
    {
        InitializeComponent();
    }
}