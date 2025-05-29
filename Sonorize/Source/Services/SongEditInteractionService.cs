using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Sonorize.Models;
using Sonorize.ViewModels; // For SongMetadataEditorViewModel

namespace Sonorize.Services;

public class SongEditInteractionService
{
    private readonly PlaybackService _playbackService;
    private readonly SongMetadataService _songMetadataService;
    private readonly ThemeColors _currentTheme;

    public SongEditInteractionService(
        PlaybackService playbackService,
        SongMetadataService songMetadataService,
        ThemeColors currentTheme)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _songMetadataService = songMetadataService ?? throw new ArgumentNullException(nameof(songMetadataService));
        _currentTheme = currentTheme ?? throw new ArgumentNullException(nameof(currentTheme));
        Debug.WriteLine("[SongEditInteractionService] Initialized.");
    }

    public async Task<(bool metadataSaved, string statusMessage)> HandleEditSongMetadataAsync(Song songToEdit, Window ownerWindow)
    {
        if (songToEdit == null)
        {
            Debug.WriteLine("[SongEditInteractionService] HandleEditSongMetadataAsync: Song is null.");
            return (false, "Error: No song selected for editing.");
        }

        if (ownerWindow == null)
        {
            Debug.WriteLine("[SongEditInteractionService] HandleEditSongMetadataAsync: Owner window is null.");
            return (false, "Error: Cannot open editor dialog without an owner window.");
        }

        (bool WasPlaying, TimeSpan Position)? previousPlaybackState = null;
        if (_playbackService.CurrentSong == songToEdit)
        {
            previousPlaybackState = _playbackService.StopAndReleaseFileResourcesForSong(songToEdit);
            if (previousPlaybackState == null)
            {
                Debug.WriteLine($"[SongEditInteractionService] Failed to release file for {songToEdit.Title}. Aborting metadata edit.");
                return (false, $"Error: Could not prepare '{songToEdit.Title}' for editing.");
            }
            Debug.WriteLine($"[SongEditInteractionService] Playback for {songToEdit.Title} stopped and file released. WasPlaying: {previousPlaybackState.Value.WasPlaying}, Position: {previousPlaybackState.Value.Position}");
        }

        bool metadataSaved = false;
        string finalStatusMessage = string.Empty;

        try
        {
            var editorViewModel = new SongMetadataEditorViewModel(songToEdit);
            var editorDialog = new Sonorize.Views.SongMetadataEditorWindow(_currentTheme)
            {
                DataContext = editorViewModel
            };
            await editorDialog.ShowDialog(ownerWindow);

            if (editorViewModel.DialogResult) // True if "Save" was clicked
            {
                Debug.WriteLine($"[SongEditInteractionService] Metadata editor for {songToEdit.Title} closed with Save. Attempting to save to file.");
                metadataSaved = await _songMetadataService.SaveMetadataAsync(songToEdit);
                if (metadataSaved)
                {
                    Debug.WriteLine($"[SongEditInteractionService] Metadata (and thumbnail) for {songToEdit.Title} saved to file successfully.");
                    finalStatusMessage = $"Metadata for '{songToEdit.Title}' updated.";
                }
                else
                {
                    Debug.WriteLine($"[SongEditInteractionService] Failed to save metadata for {songToEdit.Title} to file.");
                    finalStatusMessage = $"Error saving metadata for '{songToEdit.Title}'.";
                }
            }
            else
            {
                Debug.WriteLine($"[SongEditInteractionService] Metadata editor for {songToEdit.Title} closed without saving.");
                finalStatusMessage = "Metadata editing cancelled.";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SongEditInteractionService] Exception during metadata edit/save for {songToEdit.Title}: {ex.Message}");
            finalStatusMessage = $"Error during metadata edit for '{songToEdit.Title}'.";
            metadataSaved = false; // Ensure this is false on exception
        }
        finally
        {
            if (previousPlaybackState.HasValue)
            {
                Debug.WriteLine($"[SongEditInteractionService] Reinitializing playback for {songToEdit.Title} to WasPlaying: {previousPlaybackState.Value.WasPlaying}, Position: {previousPlaybackState.Value.Position}");
                bool reinitSuccess = _playbackService.ReinitializePlaybackForSong(songToEdit, previousPlaybackState.Value.Position, previousPlaybackState.Value.WasPlaying);
                if (!reinitSuccess)
                {
                    Debug.WriteLine($"[SongEditInteractionService] Failed to reinitialize playback for {songToEdit.Title}.");
                    // Append to status message or overwrite if more critical
                    finalStatusMessage = string.IsNullOrEmpty(finalStatusMessage) || finalStatusMessage.StartsWith("Error") ?
                                         $"Playback error after editing '{songToEdit.Title}'." :
                                         finalStatusMessage + $" (Playback error after edit)";
                }
            }
        }
        return (metadataSaved, finalStatusMessage);
    }
}