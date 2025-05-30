﻿using Avalonia.Media.Imaging;

namespace Sonorize.ViewModels;

public class ArtistViewModel : ViewModelBase // Inherit from ViewModelBase
{
    public string? Name { get; set; } // Name can remain simple if not changed after creation

    private Bitmap? _thumbnail;
    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value); // Use SetProperty for INotifyPropertyChanged
    }
    // You could add more properties later, like SongCount or AlbumCount
}