using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels;
using Sonorize.Views.MainWindowControls;

namespace Sonorize.ViewModels.Settings
{
    public class AppearanceSettingsViewModel : ViewModelBase
    {
        private readonly Action _notifyParentSettingsChanged;

        // Backing fields
        private GridViewImageType _artistGridType;
        private GridViewImageType _albumGridType;
        private GridViewImageType _playlistGridType;
        private PlaybackAreaBackgroundStyle _playbackBackgroundStyle;
        private bool _showArtistInLibrary;
        private bool _showAlbumInLibrary;
        private bool _showDurationInLibrary;
        private bool _showDateAddedInLibrary;
        private bool _showPlayCountInLibrary;
        private double _libraryRowHeight;
        private bool _enableAlternatingRowColors;
        private bool _useCompactPlaybackControls;
        private bool _showStatusBar;

        // Initial state fields
        private readonly GridViewImageType _initialArtistGridType;
        private readonly GridViewImageType _initialAlbumGridType;
        private readonly GridViewImageType _initialPlaylistGridType;
        private readonly PlaybackAreaBackgroundStyle _initialPlaybackBackgroundStyle;
        private readonly bool _initialShowArtist;
        private readonly bool _initialShowAlbum;
        private readonly bool _initialShowDuration;
        private readonly bool _initialShowDateAdded;
        private readonly bool _initialShowPlayCount;
        private readonly double _initialLibraryRowHeight;
        private readonly bool _initialEnableAlternatingRowColors;
        private readonly bool _initialUseCompactPlaybackControls;
        private readonly bool _initialShowStatusBar;

        // Properties
        public GridViewImageType ArtistGridType { get => _artistGridType; private set { if (_artistGridType != value) { _artistGridType = value; OnAppearanceChanged(); } } }
        public GridViewImageType AlbumGridType { get => _albumGridType; private set { if (_albumGridType != value) { _albumGridType = value; OnAppearanceChanged(); } } }
        public GridViewImageType PlaylistGridType { get => _playlistGridType; private set { if (_playlistGridType != value) { _playlistGridType = value; OnAppearanceChanged(); } } }
        public PlaybackAreaBackgroundStyle PlaybackBackgroundStyle { get => _playbackBackgroundStyle; private set { if (_playbackBackgroundStyle != value) { _playbackBackgroundStyle = value; OnAppearanceChanged(); } } }
        public bool ShowArtistInLibrary { get => _showArtistInLibrary; set { if (_showArtistInLibrary != value) { _showArtistInLibrary = value; OnAppearanceChanged(); } } }
        public bool ShowAlbumInLibrary { get => _showAlbumInLibrary; set { if (_showAlbumInLibrary != value) { _showAlbumInLibrary = value; OnAppearanceChanged(); } } }
        public bool ShowDurationInLibrary { get => _showDurationInLibrary; set { if (_showDurationInLibrary != value) { _showDurationInLibrary = value; OnAppearanceChanged(); } } }
        public bool ShowDateAddedInLibrary { get => _showDateAddedInLibrary; set { if (_showDateAddedInLibrary != value) { _showDateAddedInLibrary = value; OnAppearanceChanged(); } } }
        public bool ShowPlayCountInLibrary { get => _showPlayCountInLibrary; set { if (_showPlayCountInLibrary != value) { _showPlayCountInLibrary = value; OnAppearanceChanged(); } } }
        public double LibraryRowHeight { get => _libraryRowHeight; set { if (Math.Abs(_libraryRowHeight - value) > 0.01) { _libraryRowHeight = value; OnAppearanceChanged(); } } }
        public bool EnableAlternatingRowColors { get => _enableAlternatingRowColors; set { if (_enableAlternatingRowColors != value) { _enableAlternatingRowColors = value; OnAppearanceChanged(); } } }
        public bool UseCompactPlaybackControls { get => _useCompactPlaybackControls; set { if (_useCompactPlaybackControls != value) { _useCompactPlaybackControls = value; OnAppearanceChanged(); } } }
        public bool ShowStatusBar { get => _showStatusBar; set { if (_showStatusBar != value) { _showStatusBar = value; OnAppearanceChanged(); } } }

        // Radio Button Helpers
        public bool IsArtistGridSingle { get => ArtistGridType == GridViewImageType.Single; set { if (value) ArtistGridType = GridViewImageType.Single; } }
        public bool IsArtistGridComposite { get => ArtistGridType == GridViewImageType.Composite; set { if (value) ArtistGridType = GridViewImageType.Composite; } }
        public bool IsAlbumGridSingle { get => AlbumGridType == GridViewImageType.Single; set { if (value) AlbumGridType = GridViewImageType.Single; } }
        public bool IsAlbumGridComposite { get => AlbumGridType == GridViewImageType.Composite; set { if (value) AlbumGridType = GridViewImageType.Composite; } }
        public bool IsPlaylistGridSingle { get => PlaylistGridType == GridViewImageType.Single; set { if (value) PlaylistGridType = GridViewImageType.Single; } }
        public bool IsPlaylistGridComposite { get => PlaylistGridType == GridViewImageType.Composite; set { if (value) PlaylistGridType = GridViewImageType.Composite; } }
        public bool IsPlaybackBackgroundSolid { get => PlaybackBackgroundStyle == PlaybackAreaBackgroundStyle.Solid; set { if (value) PlaybackBackgroundStyle = PlaybackAreaBackgroundStyle.Solid; } }
        public bool IsPlaybackBackgroundAlbumArtStretch { get => PlaybackBackgroundStyle == PlaybackAreaBackgroundStyle.AlbumArtStretch; set { if (value) PlaybackBackgroundStyle = PlaybackAreaBackgroundStyle.AlbumArtStretch; } }
        public bool IsPlaybackBackgroundAlbumArtAbstract { get => PlaybackBackgroundStyle == PlaybackAreaBackgroundStyle.AlbumArtAbstract; set { if (value) PlaybackBackgroundStyle = PlaybackAreaBackgroundStyle.AlbumArtAbstract; } }


        public bool HasChangesFromInitialState =>
            _initialArtistGridType != ArtistGridType ||
            _initialAlbumGridType != AlbumGridType ||
            _initialPlaylistGridType != PlaylistGridType ||
            _initialPlaybackBackgroundStyle != PlaybackBackgroundStyle ||
            _initialShowArtist != ShowArtistInLibrary ||
            _initialShowAlbum != ShowAlbumInLibrary ||
            _initialShowDuration != ShowDurationInLibrary ||
            _initialShowDateAdded != ShowDateAddedInLibrary ||
            _initialShowPlayCount != ShowPlayCountInLibrary ||
            Math.Abs(_initialLibraryRowHeight - LibraryRowHeight) > 0.01 ||
            _initialEnableAlternatingRowColors != EnableAlternatingRowColors ||
            _initialUseCompactPlaybackControls != UseCompactPlaybackControls ||
            _initialShowStatusBar != ShowStatusBar;

        public AppearanceSettingsViewModel(AppearanceSettings settings, Action notifyParentSettingsChanged)
        {
            _notifyParentSettingsChanged = notifyParentSettingsChanged ?? throw new ArgumentNullException(nameof(notifyParentSettingsChanged));

            _initialArtistGridType = Enum.TryParse<GridViewImageType>(settings.ArtistGridViewImageType, out var agt) ? agt : GridViewImageType.Composite;
            _initialAlbumGridType = Enum.TryParse<GridViewImageType>(settings.AlbumGridViewImageType, out var alhgt) ? alhgt : GridViewImageType.Composite;
            _initialPlaylistGridType = Enum.TryParse<GridViewImageType>(settings.PlaylistGridViewImageType, out var pgt) ? pgt : GridViewImageType.Composite;
            _initialPlaybackBackgroundStyle = Enum.TryParse<PlaybackAreaBackgroundStyle>(settings.PlaybackAreaBackgroundStyle, out var pbs) ? pbs : PlaybackAreaBackgroundStyle.Solid;
            _initialShowArtist = settings.ShowArtistInLibrary;
            _initialShowAlbum = settings.ShowAlbumInLibrary;
            _initialShowDuration = settings.ShowDurationInLibrary;
            _initialShowDateAdded = settings.ShowDateAddedInLibrary;
            _initialShowPlayCount = settings.ShowPlayCountInLibrary;
            _initialLibraryRowHeight = settings.LibraryRowHeight;
            _initialEnableAlternatingRowColors = settings.EnableAlternatingRowColors;
            _initialUseCompactPlaybackControls = settings.UseCompactPlaybackControls;
            _initialShowStatusBar = settings.ShowStatusBar;

            _artistGridType = _initialArtistGridType;
            _albumGridType = _initialAlbumGridType;
            _playlistGridType = _initialPlaylistGridType;
            _playbackBackgroundStyle = _initialPlaybackBackgroundStyle;
            _showArtistInLibrary = _initialShowArtist;
            _showAlbumInLibrary = _initialShowAlbum;
            _showDurationInLibrary = _initialShowDuration;
            _showDateAddedInLibrary = _initialShowDateAdded;
            _showPlayCountInLibrary = _initialShowPlayCount;
            _libraryRowHeight = _initialLibraryRowHeight;
            _enableAlternatingRowColors = _initialEnableAlternatingRowColors;
            _useCompactPlaybackControls = _initialUseCompactPlaybackControls;
            _showStatusBar = _initialShowStatusBar;
        }

        private void OnAppearanceChanged([CallerMemberName] string? propertyName = null)
        {
            OnPropertyChanged(propertyName);

            // Notify dependent radio button properties
            OnPropertyChanged(nameof(IsArtistGridSingle));
            OnPropertyChanged(nameof(IsArtistGridComposite));
            OnPropertyChanged(nameof(IsAlbumGridSingle));
            OnPropertyChanged(nameof(IsAlbumGridComposite));
            OnPropertyChanged(nameof(IsPlaylistGridSingle));
            OnPropertyChanged(nameof(IsPlaylistGridComposite));
            OnPropertyChanged(nameof(IsPlaybackBackgroundSolid));
            OnPropertyChanged(nameof(IsPlaybackBackgroundAlbumArtStretch));
            OnPropertyChanged(nameof(IsPlaybackBackgroundAlbumArtAbstract));


            // Notify change detection and parent
            OnPropertyChanged(nameof(HasChangesFromInitialState));
            _notifyParentSettingsChanged();
        }

        public void UpdateAppSettings(AppearanceSettings settings)
        {
            settings.ArtistGridViewImageType = ArtistGridType.ToString();
            settings.AlbumGridViewImageType = AlbumGridType.ToString();
            settings.PlaylistGridViewImageType = PlaylistGridType.ToString();
            settings.PlaybackAreaBackgroundStyle = PlaybackBackgroundStyle.ToString();
            settings.ShowArtistInLibrary = ShowArtistInLibrary;
            settings.ShowAlbumInLibrary = ShowAlbumInLibrary;
            settings.ShowDurationInLibrary = ShowDurationInLibrary;
            settings.ShowDateAddedInLibrary = ShowDateAddedInLibrary;
            settings.ShowPlayCountInLibrary = ShowPlayCountInLibrary;
            settings.LibraryRowHeight = LibraryRowHeight;
            settings.EnableAlternatingRowColors = EnableAlternatingRowColors;
            settings.UseCompactPlaybackControls = UseCompactPlaybackControls;
            settings.ShowStatusBar = ShowStatusBar;
        }
    }
}