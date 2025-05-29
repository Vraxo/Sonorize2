using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels.MainWindow;

public class MainWindowInteractionCoordinator
{
    private readonly Func<Window?> _getOwnerViewFunc;
    private readonly LibraryViewModel _libraryViewModel;
    private readonly AdvancedPanelViewModel _advancedPanelViewModel;
    private readonly ApplicationWorkflowManager _workflowManager;
    private readonly SongEditInteractionService _songEditInteractionService;
    private readonly Action _raiseAllCommandsCanExecuteChangedCallback;

    public MainWindowInteractionCoordinator(
        Func<Window?> getOwnerViewFunc,
        LibraryViewModel libraryViewModel,
        AdvancedPanelViewModel advancedPanelViewModel,
        ApplicationWorkflowManager workflowManager,
        SongEditInteractionService songEditInteractionService,
        Action raiseAllCommandsCanExecuteChangedCallback)
    {
        _getOwnerViewFunc = getOwnerViewFunc ?? throw new ArgumentNullException(nameof(getOwnerViewFunc));
        _libraryViewModel = libraryViewModel ?? throw new ArgumentNullException(nameof(libraryViewModel));
        _advancedPanelViewModel = advancedPanelViewModel ?? throw new ArgumentNullException(nameof(advancedPanelViewModel));
        _workflowManager = workflowManager ?? throw new ArgumentNullException(nameof(workflowManager));
        _songEditInteractionService = songEditInteractionService ?? throw new ArgumentNullException(nameof(songEditInteractionService));
        _raiseAllCommandsCanExecuteChangedCallback = raiseAllCommandsCanExecuteChangedCallback ?? throw new ArgumentNullException(nameof(raiseAllCommandsCanExecuteChangedCallback));
    }

    public async Task<string> CoordinateOpenSettingsDialogAsync()
    {
        Window? owner = _getOwnerViewFunc();
        if (owner == null || _libraryViewModel.IsLoadingLibrary)
        {
            return "Cannot open settings: No owner window or library is loading.";
        }

        _advancedPanelViewModel.IsVisible = false;

        var (statusMessages, settingsChanged) = await _workflowManager.HandleOpenSettingsDialogAsync(owner);

        string resultMessage = string.Empty;
        if (settingsChanged)
        {
            if (statusMessages.Any())
            {
                resultMessage = string.Join(" | ", statusMessages);
            }
            // If no specific messages, the caller (MainWindowViewModel) will update status bar.
        }

        _raiseAllCommandsCanExecuteChangedCallback();
        return resultMessage;
    }

    public async Task<(bool RefreshedNeeded, string StatusMessage)> CoordinateAddMusicDirectoryAsync()
    {
        Window? owner = _getOwnerViewFunc();
        if (owner == null || _libraryViewModel.IsLoadingLibrary)
        {
            return (false, "Cannot add directory: No owner window or library is loading.");
        }
        _advancedPanelViewModel.IsVisible = false;

        var (directoryAdded, statusMessage) = await _workflowManager.HandleAddMusicDirectoryAsync(owner);

        _raiseAllCommandsCanExecuteChangedCallback();
        return (directoryAdded, statusMessage);
    }

    public async Task<string> CoordinateEditSongMetadataAsync(Song? songToEdit)
    {
        if (songToEdit == null)
        {
            Debug.WriteLine("[InteractionCoordinator] EditSongMetadata: songToEdit is null.");
            return "No song selected to edit.";
        }

        Window? ownerWindow = _getOwnerViewFunc();
        if (ownerWindow == null)
        {
            Debug.WriteLine("[InteractionCoordinator] EditSongMetadata: Owner window is not set.");
            return "Error: Cannot open editor, main window context lost.";
        }

        _advancedPanelViewModel.IsVisible = false;

        var (metadataSaved, statusMessage) = await _songEditInteractionService.HandleEditSongMetadataAsync(songToEdit, ownerWindow);

        _raiseAllCommandsCanExecuteChangedCallback();
        return statusMessage;
    }
}