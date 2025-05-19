using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Sonorize.Controls;

public partial class SearchBarControl : UserControl
{
    public SearchBarControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}