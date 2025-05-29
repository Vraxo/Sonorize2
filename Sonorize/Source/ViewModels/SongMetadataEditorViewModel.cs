using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using Sonorize.Models;
using Sonorize.Services; // For PlaybackService
using TagLib;

namespace Sonorize.ViewModels;

public class SongMetadataEditorViewModel : ViewModelBase
{
    private readonly Song _originalSong;
    private readonly PlaybackService _playbackService;

    public string WindowTitle => $"Edit Metadata - {_originalSong.Title}";

    private string _editableTitle;
    public string EditableTitle
    {
        get => _editableTitle;
        set => SetProperty(ref _editableTitle, value);
    }

    private string _editableArtist;
    public string EditableArtist
    {
        get => _editableArtist;
        set => SetProperty(ref _editableArtist, value);
    }

    private string _editableAlbum;
    public string EditableAlbum
    {
        get => _editableAlbum;
        set => SetProperty(ref _editableAlbum, value);
    }

    private string _editableGenre;
    public string EditableGenre
    {
        get => _editableGenre;
        set => SetProperty(ref _editableGenre, value);
    }

    private string _editableTrackNumber;
    public string EditableTrackNumber // String for easier binding and validation
    {
        get => _editableTrackNumber;
        set => SetProperty(ref _editableTrackNumber, value);
    }

    private string _editableYear;
    public string EditableYear // String for easier binding and validation
    {
        get => _editableYear;
        set => SetProperty(ref _editableYear, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public Action<bool>? CloseAction { get; set; }


    public SongMetadataEditorViewModel(Song originalSong, PlaybackService playbackService)
    {
        _originalSong = originalSong ?? throw new ArgumentNullException(nameof(originalSong));
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));

        _editableTitle = _originalSong.Title;
        _editableArtist = _originalSong.Artist;
        _editableAlbum = _originalSong.Album;
        _editableGenre = _originalSong.Genre;
        _editableTrackNumber = _originalSong.TrackNumber == 0 ? string.Empty : _originalSong.TrackNumber.ToString(CultureInfo.InvariantCulture);
        _editableYear = _originalSong.Year == 0 ? string.Empty : _originalSong.Year.ToString(CultureInfo.InvariantCulture);

        SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
        CancelCommand = new RelayCommand(ExecuteCancel);
    }

    private bool CanExecuteSave(object? parameter)
    {
        // Check if the song is currently playing
        if (_playbackService.CurrentSong == _originalSong && _playbackService.IsPlaying)
        {
            return false; // Don't allow saving if the song is playing
        }
        return true;
    }


    private void ExecuteSave(object? parameter)
    {
        try
        {
            using (var tagFile = TagLib.File.Create(_originalSong.FilePath))
            {
                tagFile.Tag.Title = EditableTitle;
                tagFile.Tag.Performers = new[] { EditableArtist };
                tagFile.Tag.AlbumArtists = new[] { EditableArtist };
                tagFile.Tag.Album = EditableAlbum;
                tagFile.Tag.Genres = new[] { EditableGenre };

                if (uint.TryParse(EditableTrackNumber, out uint trackNum))
                {
                    tagFile.Tag.Track = trackNum;
                }
                else
                {
                    tagFile.Tag.Track = 0; // Or handle error
                }

                if (uint.TryParse(EditableYear, out uint yearNum))
                {
                    tagFile.Tag.Year = yearNum;
                }
                else
                {
                    tagFile.Tag.Year = 0; // Or handle error
                }

                tagFile.Save();
                Debug.WriteLine($"[SongMetadataEditorVM] Successfully saved metadata for: {_originalSong.FilePath}");

                // Update the original Song object
                _originalSong.Title = EditableTitle;
                _originalSong.Artist = EditableArtist;
                _originalSong.Album = EditableAlbum;
                _originalSong.Genre = EditableGenre;
                _originalSong.TrackNumber = uint.TryParse(EditableTrackNumber, out trackNum) ? trackNum : 0;
                _originalSong.Year = uint.TryParse(EditableYear, out yearNum) ? yearNum : 0;

                CloseAction?.Invoke(true);
            }
        }
        catch (IOException ioEx)
        {
            Debug.WriteLine($"[SongMetadataEditorVM] IO Error saving metadata for {_originalSong.FilePath}: {ioEx.Message}. File might be in use.");
            // TODO: Show error message to user
            CloseAction?.Invoke(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SongMetadataEditorVM] Error saving metadata for {_originalSong.FilePath}: {ex.Message}");
            // TODO: Show error message to user
            CloseAction?.Invoke(false);
        }
    }

    private void ExecuteCancel(object? parameter)
    {
        CloseAction?.Invoke(false);
    }

    public void RefreshCanExecuteSave()
    {
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    // Listen to PlaybackService to enable/disable save command
    public void SubscribeToPlaybackServiceEvents()
    {
        _playbackService.PropertyChanged += PlaybackService_PropertyChanged;
        RefreshCanExecuteSave();
    }

    public void UnsubscribeFromPlaybackServiceEvents()
    {
        _playbackService.PropertyChanged -= PlaybackService_PropertyChanged;
    }

    private void PlaybackService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaybackService.CurrentSong) || e.PropertyName == nameof(PlaybackService.IsPlaying))
        {
            RefreshCanExecuteSave();
        }
    }
}