using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Sonorize.Models;
using Sonorize.ViewModels; // Required for LibraryViewModel

namespace Sonorize.Services;

public class SettingsChangeProcessorService
{
    private readonly LibraryViewModel _libraryViewModel;
    private readonly ScrobblingService _scrobblingService;

    public SettingsChangeProcessorService(LibraryViewModel libraryViewModel, ScrobblingService scrobblingService)
    {
        _libraryViewModel = libraryViewModel ?? throw new System.ArgumentNullException(nameof(libraryViewModel));
        _scrobblingService = scrobblingService ?? throw new System.ArgumentNullException(nameof(scrobblingService));
    }

    public async Task<List<string>> ProcessChangesAndGetStatus(AppSettings oldSettings, AppSettings newSettings)
    {
        List<string> statusMessages = [];

        // Directory changes
        bool dirsActuallyChanged = !oldSettings.MusicDirectories.SequenceEqual(newSettings.MusicDirectories);
        if (dirsActuallyChanged)
        {
            Debug.WriteLine("[SettingsChangeProcessor] Music directories changed. Reloading library.");
            await _libraryViewModel.LoadLibraryAsync();
            // Status text during library loading is handled by LibraryViewModel/MusicLibraryService
        }

        // Theme changes
        bool themeActuallyChanged = oldSettings.PreferredThemeFileName != newSettings.PreferredThemeFileName;
        if (themeActuallyChanged)
        {
            Debug.WriteLine("[SettingsChangeProcessor] Theme changed. Restart recommended.");
            statusMessages.Add("Theme changed. Restart Sonorize for full effect.");
        }

        // Scrobbling settings changes
        bool scrobbleSettingsActuallyChanged =
            oldSettings.LastfmScrobblingEnabled != newSettings.LastfmScrobblingEnabled ||
            oldSettings.LastfmUsername != newSettings.LastfmUsername ||
            oldSettings.LastfmPassword != newSettings.LastfmPassword || // Used for change detection only
            oldSettings.LastfmSessionKey != newSettings.LastfmSessionKey || // If session key is cleared/changed directly
            oldSettings.ScrobbleThresholdPercentage != newSettings.ScrobbleThresholdPercentage ||
            oldSettings.ScrobbleThresholdAbsoluteSeconds != newSettings.ScrobbleThresholdAbsoluteSeconds;

        if (scrobbleSettingsActuallyChanged)
        {
            Debug.WriteLine("[SettingsChangeProcessor] Scrobbling settings changed. Refreshing ScrobblingService.");
            _scrobblingService.RefreshSettings(); // This will re-evaluate credentials and session key

            // Provide feedback based on the new state of scrobbling AFTER refresh
            _scrobblingService.RefreshSettings(); // Call refresh again to ensure service state is based on latest
            if (_scrobblingService.IsScrobblingEnabled && _scrobblingService.AreCredentialsEffectivelyConfigured)
            {
                // Check if already added "Theme changed..." to avoid overwriting it with a less critical message
                if (!statusMessages.Any(m => m.Contains("Theme changed")))
                {
                    statusMessages.Add("Scrobbling enabled and configured.");
                }
                else
                {
                    // Append if theme change message exists
                    var themeMsgIndex = statusMessages.FindIndex(m => m.Contains("Theme changed"));
                    if (themeMsgIndex != -1) statusMessages[themeMsgIndex] += " Scrobbling enabled.";
                    else statusMessages.Add("Scrobbling enabled and configured.");
                }
            }
            else if (_scrobblingService.IsScrobblingEnabled && !_scrobblingService.AreCredentialsEffectivelyConfigured)
            {
                if (!statusMessages.Any(m => m.Contains("Theme changed")))
                {
                    statusMessages.Add("Scrobbling enabled, but not configured. Check settings.");
                }
                else
                {
                    var themeMsgIndex = statusMessages.FindIndex(m => m.Contains("Theme changed"));
                    if (themeMsgIndex != -1) statusMessages[themeMsgIndex] += " Scrobbling enabled (check config).";
                    else statusMessages.Add("Scrobbling enabled, but not configured. Check settings.");
                }
            }
            else if (!_scrobblingService.IsScrobblingEnabled && oldSettings.LastfmScrobblingEnabled)
            {
                if (!statusMessages.Any(m => m.Contains("Theme changed")))
                {
                    statusMessages.Add("Scrobbling disabled.");
                }
                else
                {
                    var themeMsgIndex = statusMessages.FindIndex(m => m.Contains("Theme changed"));
                    if (themeMsgIndex != -1) statusMessages[themeMsgIndex] += " Scrobbling disabled.";
                    else statusMessages.Add("Scrobbling disabled.");
                }

            }
        }

        // If no specific messages were generated but changes happened, ensure status bar updates.
        // This is implicitly handled if statusMessages is empty, MainWindowViewModel calls UpdateStatusBarText().

        return statusMessages;
    }
}