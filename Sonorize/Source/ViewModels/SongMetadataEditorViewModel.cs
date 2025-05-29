using System.Windows.Input;
using Sonorize.Models;
using Avalonia.Media.Imaging; // For Bitmap
using System.IO; // For MemoryStream, File
using Avalonia.Platform.Storage; // For FilePicker
using Avalonia.Controls; // For Window (as command parameter)
using System.Linq; // For FirstOrDefault
using System.Diagnostics; // For Debug

namespace Sonorize.ViewModels;

public class SongMetadataEditorViewModel : ViewModelBase
{
    public Song SongToEdit { get; }

    private Bitmap? _currentDisplayThumbnail;
    public Bitmap? CurrentDisplayThumbnail
    {
        get => _currentDisplayThumbnail;
        private set => SetProperty(ref _currentDisplayThumbnail, value);
    }

    public string Title
    {
        get => SongToEdit.Title;
        set
        {
            if (SongToEdit.Title != value)
            {
                SongToEdit.Title = value;
                OnPropertyChanged();
            }
        }
    }

    public string Artist
    {
        get => SongToEdit.Artist;
        set
        {
            if (SongToEdit.Artist != value)
            {
                SongToEdit.Artist = value;
                OnPropertyChanged();
            }
        }
    }

    public string Album
    {
        get => SongToEdit.Album;
        set
        {
            if (SongToEdit.Album != value)
            {
                SongToEdit.Album = value;
                OnPropertyChanged();
            }
        }
    }

    public bool DialogResult { get; private set; } = false;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ChangeThumbnailCommand { get; }

    public System.Action? CloseWindowAction { get; set; }


    public SongMetadataEditorViewModel(Song songToEdit)
    {
        SongToEdit = songToEdit ?? throw new System.ArgumentNullException(nameof(songToEdit));
        _currentDisplayThumbnail = songToEdit.Thumbnail; // Initialize with current song's thumbnail

        SaveCommand = new RelayCommand(_ =>
        {
            SongToEdit.Thumbnail = CurrentDisplayThumbnail; // Commit the potentially changed thumbnail
            DialogResult = true;
            CloseWindowAction?.Invoke();
        });

        CancelCommand = new RelayCommand(_ =>
        {
            DialogResult = false;
            CloseWindowAction?.Invoke();
        });

        ChangeThumbnailCommand = new RelayCommand(async owner =>
        {
            if (owner is not Window ownerWindow) return;

            var filePickerOptions = new FilePickerOpenOptions
            {
                Title = "Select Cover Image",
                AllowMultiple = false,
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
            };

            var result = await ownerWindow.StorageProvider.OpenFilePickerAsync(filePickerOptions);
            if (result.Count > 0)
            {
                var selectedFile = result.FirstOrDefault();
                if (selectedFile != null)
                {
                    try
                    {
                        await using var stream = await selectedFile.OpenReadAsync();
                        CurrentDisplayThumbnail = new Bitmap(stream);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.WriteLine($"[SongMetaEditorVM] Error loading image: {ex.Message}");
                        // Optionally show an error to the user
                    }
                }
            }
        });
    }
}