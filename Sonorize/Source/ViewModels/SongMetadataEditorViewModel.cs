using System.Windows.Input;
using Sonorize.Models;

namespace Sonorize.ViewModels;

public class SongMetadataEditorViewModel : ViewModelBase
{
    public Song SongToEdit { get; }

    public string Title
    {
        get => SongToEdit.Title;
        set
        {
            if (SongToEdit.Title != value)
            {
                SongToEdit.Title = value;
                OnPropertyChanged(); // Notify UI
                // No need to call SetProperty directly on SongToEdit here as its own setter handles it
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

    // Action to be called by the window when closing via Save/Cancel
    public System.Action? CloseWindowAction { get; set; }


    public SongMetadataEditorViewModel(Song songToEdit)
    {
        SongToEdit = songToEdit ?? throw new System.ArgumentNullException(nameof(songToEdit));

        SaveCommand = new RelayCommand(_ =>
        {
            DialogResult = true;
            CloseWindowAction?.Invoke();
        });

        CancelCommand = new RelayCommand(_ =>
        {
            DialogResult = false;
            CloseWindowAction?.Invoke();
        });
    }
}