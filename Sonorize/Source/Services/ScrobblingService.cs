using System;
using System.Diagnostics;
using System.Threading.Tasks;
using IF.Lastfm.Core.Api;
using IF.Lastfm.Core.Api.Helpers;
using IF.Lastfm.Core.Objects; // Added for service classes like Track, Auth
using Sonorize.Models;

namespace Sonorize.Services;

public class ScrobblingService
{
    private readonly SettingsService _settingsService;
    private readonly LastfmAuthenticatorService _authenticatorService;
    private AppSettings _currentSettings;

    public bool IsScrobblingEnabled => _currentSettings.Lastfm.ScrobblingEnabled;

    public bool AreCredentialsEffectivelyConfigured
    {
        get
        {
            return LastfmAuthenticatorService.AreCredentialsEffectivelyConfigured(_currentSettings);
        }
    }

    public ScrobblingService(
        SettingsService settingsService,
        LastfmAuthenticatorService authenticatorService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _authenticatorService = authenticatorService ?? throw new ArgumentNullException(nameof(authenticatorService));
        RefreshSettings(); // Initial load of settings
        Debug.WriteLine($"[ScrobblingService] Initialized. Scrobbling Enabled: {IsScrobblingEnabled}, Credentials Configured: {AreCredentialsEffectivelyConfigured}");
    }

    public void RefreshSettings()
    {
        _currentSettings = _settingsService.LoadSettings();
        Debug.WriteLine($"[ScrobblingService] Settings refreshed. " +
            $"Scrobbling Enabled: {IsScrobblingEnabled}, " +
            $"Credentials Configured: {AreCredentialsEffectivelyConfigured}, " +
            $"Thresholds: {_currentSettings.Lastfm.ScrobbleThresholdPercentage}% / {_currentSettings.Lastfm.ScrobbleThresholdAbsoluteSeconds}s");
    }

    private async Task<LastfmClient?> GetAuthenticatedClientAsync()
    {
        // _authenticatorService.GetAuthenticatedClientAsync() will load settings itself if needed.
        // No need to call RefreshSettings() here as it would be redundant.
        return await _authenticatorService.GetAuthenticatedClientAsync();
    }

    public bool ShouldScrobble(Song song, TimeSpan playedDuration)
    {
        // Rely on _currentSettings being up-to-date from constructor or external RefreshSettings call.
        var thresholds = new ScrobbleThresholds(
            _currentSettings.Lastfm.ScrobbleThresholdPercentage,
            _currentSettings.Lastfm.ScrobbleThresholdAbsoluteSeconds);

        return ScrobbleEligibilityService.ShouldScrobble(song, playedDuration, thresholds);
    }


    public async Task UpdateNowPlayingAsync(Song song)
    {
        // Rely on _currentSettings being up-to-date.
        if (!IsScrobblingEnabled || song == null)
        {
            Debug.WriteLine($"[ScrobblingService] UpdateNowPlayingAsync skipped. Enabled: {IsScrobblingEnabled}, Song: {song?.Title ?? "null"}");
            return;
        }

        // FINAL SAFEGUARD: Last.fm requires artist and title for scrobbles.
        if (string.IsNullOrWhiteSpace(song.Artist) || string.IsNullOrWhiteSpace(song.Title))
        {
            Debug.WriteLine($"[ScrobblingService] UpdateNowPlayingAsync skipped for '{song.FilePath}'. Reason: Artist or Title metadata is missing.");
            return;
        }

        var client = await GetAuthenticatedClientAsync();
        if (client == null)
        {
            Debug.WriteLine("[ScrobblingService] UpdateNowPlayingAsync: No authenticated client. Skipping.");
            return;
        }

        try
        {
            LastResponse? response = null;
            for (int i = 0; i < 2; i++) // Retry once on failure
            {
                Debug.WriteLine($"[ScrobblingService] Sending UpdateNowPlaying for: {song.Title} by {song.Artist} (Attempt {i + 1})");
                var scrobble = new Scrobble(song.Artist, song.Album, song.Title, DateTimeOffset.Now);
                response = await client.Track.UpdateNowPlayingAsync(scrobble);

                if (response.Success)
                {
                    Debug.WriteLine($"[ScrobblingService] UpdateNowPlaying successful for: {song.Title}");
                    break; // Exit loop on success
                }
                else
                {
                    Debug.WriteLine($"[ScrobblingService] UpdateNowPlaying FAILED for: {song.Title}. Error: {response.Status} - {response.Error}");
                    if (i == 0) // If it's the first attempt
                    {
                        Debug.WriteLine("[ScrobblingService] Waiting 2 seconds before retry...");
                        await Task.Delay(2000);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ScrobblingService] CRITICAL EXCEPTION during UpdateNowPlaying for {song.Title}: {ex.Message}");
        }
    }

    public async Task ScrobbleAsync(Song song, DateTime timePlayed)
    {
        // Rely on _currentSettings being up-to-date.
        if (!IsScrobblingEnabled || song == null)
        {
            Debug.WriteLine($"[ScrobblingService] ScrobbleAsync skipped. Enabled: {IsScrobblingEnabled}, Song: {song?.Title ?? "null"}");
            return;
        }

        // FINAL SAFEGUARD: Last.fm requires artist and title for scrobbles.
        if (string.IsNullOrWhiteSpace(song.Artist) || string.IsNullOrWhiteSpace(song.Title))
        {
            Debug.WriteLine($"[ScrobblingService] ScrobbleAsync skipped for '{song.FilePath}'. Reason: Artist or Title metadata is missing.");
            return;
        }

        var client = await GetAuthenticatedClientAsync();
        if (client == null)
        {
            Debug.WriteLine("[ScrobblingService] ScrobbleAsync: No authenticated client. Skipping.");
            return;
        }

        try
        {
            LastResponse? response = null;
            for (int i = 0; i < 2; i++) // Retry once on failure
            {
                Debug.WriteLine($"[ScrobblingService] Sending Scrobble for: {song.Title} by {song.Artist}, TimePlayed: {timePlayed} (Attempt {i + 1})");
                var scrobble = new Scrobble(song.Artist, song.Album, song.Title, timePlayed);
                response = await client.Track.ScrobbleAsync(scrobble);

                if (response.Success)
                {
                    Debug.WriteLine($"[ScrobblingService] Scrobble successful for: {song.Title}");
                    break; // Exit loop on success
                }
                else
                {
                    Debug.WriteLine($"[ScrobblingService] Scrobble FAILED for: {song.Title}. Error: {response.Status} - {response.Error}");
                    if (i == 0) // If it's the first attempt
                    {
                        Debug.WriteLine("[ScrobblingService] Waiting 2 seconds before retry...");
                        await Task.Delay(2000);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ScrobblingService] CRITICAL EXCEPTION during Scrobble for {song.Title}: {ex.Message}");
        }
    }
}