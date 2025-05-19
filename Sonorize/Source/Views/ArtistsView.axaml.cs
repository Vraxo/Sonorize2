using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Sonorize.Views;

public partial class ArtistsView : UserControl
{
    public ArtistsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}