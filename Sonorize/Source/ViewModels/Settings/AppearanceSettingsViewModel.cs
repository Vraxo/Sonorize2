using System;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels.Settings
{
    public class AppearanceSettingsViewModel : ViewModelBase
    {
        private readonly Action _notifyParentSettingsChanged;

        public GridViewImageType ArtistGridType { get; set; }
        public GridViewImageType AlbumGridType { get; set; }
        public GridViewImageType PlaylistGridType { get; set; }
        public bool ShowArtistInLibrary { get; set; }
        public bool ShowAlbumInLibrary { get; set; }
        public bool ShowDurationInLibrary { get; set; }

        private readonly GridViewImageType _initialArtistGridType;
        private readonly GridViewImageType _initialAlbumGridType;
        private readonly GridViewImageType _initialPlaylistGridType;
        private readonly bool _initialShowArtist;
        private readonly bool _initialShowAlbum;
        private readonly bool _initialShowDuration;
        
        public bool IsArtistGridSingle
        {
            get => ArtistGridType == GridViewImageType.Single;
            set { if (value && ArtistGridType != GridViewImageType.Single) { ArtistGridType = GridViewImageType.Single; OnAppearanceChanged(); } }
        }
        public bool IsArtistGridComposite
        {
            get => ArtistGridType == GridViewImageType.Composite;
            set { if (value && ArtistGridType != GridViewImageType.Composite) { ArtistGridType = GridViewImageType.Composite; OnAppearanceChanged(); } }
        }

        public bool IsAlbumGridSingle
        {
            get => AlbumGridType == GridViewImageType.Single;
            set { if (value && AlbumGridType != GridViewImageType.Single) { AlbumGridType = GridViewImageType.Single; OnAppearanceChanged(); } }
        }
        public bool IsAlbumGridComposite
        {
            get => AlbumGridType == GridViewImageType.Composite;
            set { if (value && AlbumGridType != GridViewImageType.Composite) { AlbumGridType = GridViewImageType.Composite; OnAppearanceChanged(); } }
        }

        public bool IsPlaylistGridSingle
        {
            get => PlaylistGridType == GridViewImageType.Single;
            set { if (value && PlaylistGridType != GridViewImageType.Single) { PlaylistGridType = GridViewImageType.Single; OnAppearanceChanged(); } }
        }
        public bool IsPlaylistGridComposite
        {
            get => PlaylistGridType == GridViewImageType.Composite;
            set { if (value && PlaylistGridType != GridViewImageType.Composite) { PlaylistGridType = GridViewImageType.Composite; OnAppearanceChanged(); } }
        }

        public bool HasChangesFromInitialState => _initialArtistGridType != ArtistGridType ||
                                                  _initialAlbumGridType != AlbumGridType ||
                                                  _initialPlaylistGridType != PlaylistGridType ||
                                                  _initialShowArtist != ShowArtistInLibrary ||
                                                  _initialShowAlbum != ShowAlbumInLibrary ||
                                                  _initialShowDuration != ShowDurationInLibrary;

        public AppearanceSettingsViewModel(AppSettings settings, Action notifyParentSettingsChanged)
        {
            _notifyParentSettingsChanged = notifyParentSettingsChanged ?? throw new ArgumentNullException(nameof(notifyParentSettingsChanged));
            
            _initialArtistGridType = Enum.TryParse<GridViewImageType>(settings.ArtistGridViewImageType, out var agt) ? agt : GridViewImageType.Composite;
            _initialAlbumGridType = Enum.TryParse<GridViewImageType>(settings.AlbumGridViewImageType, out var alhgt) ? alhgt : GridViewImageType.Composite;
            _initialPlaylistGridType = Enum.TryParse<GridViewImageType>(settings.PlaylistGridViewImageType, out var pgt) ? pgt : GridViewImageType.Composite;
            _initialShowArtist = settings.ShowArtistInLibrary;
            _initialShowAlbum = settings.ShowAlbumInLibrary;
            _initialShowDuration = settings.ShowDurationInLibrary;

            ArtistGridType = _initialArtistGridType;
            AlbumGridType = _initialAlbumGridType;
            PlaylistGridType = _initialPlaylistGridType;
            ShowArtistInLibrary = _initialShowArtist;
            ShowAlbumInLibrary = _initialShowAlbum;
            ShowDurationInLibrary = _initialShowDuration;
        }

        private void OnAppearanceChanged()
        {
            OnPropertyChanged(nameof(IsArtistGridSingle));
            OnPropertyChanged(nameof(IsArtistGridComposite));
            OnPropertyChanged(nameof(IsAlbumGridSingle));
            OnPropertyChanged(nameof(IsAlbumGridComposite));
            OnPropertyChanged(nameof(IsPlaylistGridSingle));
            OnPropertyChanged(nameof(IsPlaylistGridComposite));
            OnPropertyChanged(nameof(ShowArtistInLibrary));
            OnPropertyChanged(nameof(ShowAlbumInLibrary));
            OnPropertyChanged(nameof(ShowDurationInLibrary));
            OnPropertyChanged(nameof(HasChangesFromInitialState));
            _notifyParentSettingsChanged();
        }

        public void UpdateAppSettings(AppSettings settings)
        {
            settings.ArtistGridViewImageType = ArtistGridType.ToString();
            settings.AlbumGridViewImageType = AlbumGridType.ToString();
            settings.PlaylistGridViewImageType = PlaylistGridType.ToString();
            settings.ShowArtistInLibrary = ShowArtistInLibrary;
            settings.ShowAlbumInLibrary = ShowAlbumInLibrary;
            settings.ShowDurationInLibrary = ShowDurationInLibrary;
        }
    }
}
