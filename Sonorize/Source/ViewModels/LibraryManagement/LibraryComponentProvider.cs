using Sonorize.Services;

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryComponentProvider
{
    public LibraryGroupingsViewModel Groupings { get; }
    public LibraryFilterStateManager FilterState { get; }
    public SongListManager SongList { get; }
    public LibraryDataOrchestrator DataOrchestrator { get; }
    public LibraryStatusTextGenerator StatusTextGenerator { get; }
    public AutoPlaylistManager AutoPlaylistManager { get; }

    private readonly SongFilteringService _songFilteringService;

    public LibraryComponentProvider(MusicLibraryService musicLibraryService, SettingsService settingsService)
    {
        Groupings = new(musicLibraryService);
        FilterState = new();
        _songFilteringService = new();
        SongList = new(_songFilteringService);
        AutoPlaylistManager = new AutoPlaylistManager(Groupings.PlaylistManager, SongList, musicLibraryService);
        DataOrchestrator = new(musicLibraryService, Groupings.ArtistAlbumManager, settingsService, AutoPlaylistManager);
        StatusTextGenerator = new();
    }
}