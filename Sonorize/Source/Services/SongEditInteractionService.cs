using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Sonorize.Models;
using Sonorize.Services.Playback; // For PlaybackResourceInterlockService
using Sonorize.ViewModels; // For SongMetadataEditorViewModel

namespace Sonorize.Services;

public class SongEditInteractionService
{
    private readonly PlaybackResourceInterlockService _playbackResourceInterlock;
    private readonly SongMetadataService _songMetadataService;
    private readonly ThemeColors _currentTheme;

    public SongEditInteractionService(
        PlaybackResourceInterlockService playbackResourceInterlock,
        SongMetadataService songMetadataService,
        ThemeColors currentTheme)
    {
        _playbackResourceInterlock = playbackResourceInterlock ?? throw new ArgumentNullException(nameof(playbackResourceInterlock));
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

        // Use PlaybackResourceInterlockService to prepare for editing
        previousPlaybackState = _playbackResourceInterlock.PrepareForExternalOperation(songToEdit);

        if (previousPlaybackState == null && _playbackResourceInterlock.PrepareForExternalOperation(songToEdit) is not null) // Check if the song was the one playing
        {
            // This case implies the song *was* current, but PrepareForExternalOperation failed to return state (should not happen if song matches)
            // Or, more likely, if the songToEdit was NOT the current song, previousPlaybackState would be null.
            // The logic of PrepareForExternalOperation already handles if the song is not current.
            // If previousPlaybackState is null it means either the song wasn't current, or an issue occurred.
            // If it was current and an issue occurred, _playbackResourceInterlock.PrepareForExternalOperation might log it.
            // For this method, if previousPlaybackState is null after call, it means we couldn't (or didn't need to) get its state.
            // If it *was* the current song but something went wrong in Prepare, we should not proceed.
            // The check inside PrepareForExternalOperation `if (_sessionManager.CurrentSong != song)` handles non-current songs returning null.
            // So, if it's null here, it was either not the current song, or songToEdit was null (already checked).
            // If it *was* the current song and PrepareForExternalOperation returned null due to an *internal error* in Prepare,
            // that's an issue for PrepareForExternalOperation to log.
            // From this method's perspective, if previousPlaybackState is not null, it means the song was current and resources were released.
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

            if (editorViewModel.DialogResult)
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
            metadataSaved = false;
        }
        finally
        {
            if (previousPlaybackState.HasValue)
            {
                Debug.WriteLine($"[SongEditInteractionService] Reinitializing playback for {songToEdit.Title} to WasPlaying: {previousPlaybackState.Value.WasPlaying}, Position: {previousPlaybackState.Value.Position}");
                // Use PlaybackResourceInterlockService to resume playback
                bool reinitSuccess = _playbackResourceInterlock.ResumeAfterExternalOperation(songToEdit, previousPlaybackState.Value.Position, previousPlaybackState.Value.WasPlaying);
                if (!reinitSuccess)
                {
                    Debug.WriteLine($"[SongEditInteractionService] Failed to reinitialize playback for {songToEdit.Title}.");
                    finalStatusMessage = string.IsNullOrEmpty(finalStatusMessage) || finalStatusMessage.StartsWith("Error") ?
                                         $"Playback error after editing '{songToEdit.Title}'." :
                                         finalStatusMessage + $" (Playback error after edit)";
                }
            }
        }
        return (metadataSaved, finalStatusMessage);
    }
}