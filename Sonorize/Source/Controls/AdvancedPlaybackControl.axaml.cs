using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity; // Required for RoutedEventArgs
using Sonorize.ViewModels; // Required for MainWindowViewModel
using System; // Required for TimeSpan

namespace Sonorize.Controls;

public partial class AdvancedPlaybackControl : UserControl
{
    public AdvancedPlaybackControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // Event handler for the WaveformDisplayControl's SeekRequested event
    private void WaveformDisplay_SeekRequested(object? sender, TimeSpan e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.WaveformSeekCommand.Execute(e);
        }
    }
}