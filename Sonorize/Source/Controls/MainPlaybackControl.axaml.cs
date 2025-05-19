using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity; // Required for RoutedEventArgs
using Sonorize.Models; // Required for PlaybackStateStatus
using Sonorize.ViewModels;
using Sonorize.Services; // Required for MainWindowViewModel

namespace Sonorize.Controls;

public partial class MainPlaybackControl : UserControl
{
    public MainPlaybackControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // Event handler for the Play/Pause button click
    private void PlayPauseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (vm.PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Playing)
                vm.PlaybackService.Pause();
            else
                vm.PlaybackService.Resume();
        }
    }
}