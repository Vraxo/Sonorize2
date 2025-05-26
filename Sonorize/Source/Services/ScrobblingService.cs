using System;
using System.Diagnostics;
using System.Threading.Tasks;
using IF.Lastfm.Core.Api;
using IF.Lastfm.Core.Objects; // Added for service classes like Track, Auth
using Sonorize.Models;

namespace Sonorize.Services;

public class ScrobblingService
{
    private readonly SettingsService _settingsService;
    private readonly LastfmAuthenticatorService _authenticatorService;
    private AppSettings _currentSettings;

    private const int MinTrackLengthForScrobbleSeconds = 30;

    public bool IsScrobblingEnabled => _currentSettings.LastfmScrobblingEnabled;

    public bool AreCredentialsEffectivelyConfigured => _authenticatorService.AreCredentialsEffectivelyConfigured(_currentSettings);

    public ScrobblingService(SettingsService settingsService, LastfmAuthenticatorService authenticatorService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _authenticatorService = authenticatorService ?? throw new ArgumentNullException(nameof(authenticatorService));
        RefreshSettings(); // Initial load of settings
        Debug.WriteLine($"[ScrobblingService] Initialized. Scrobbling Enabled: {IsScrobblingEnabled}, Credentials Configured: {AreCredentialsEffectivelyConfigured}");
    }

    public void RefreshSettings()
    {
        _currentSettings = _settingsService.LoadSettings();
        // Session key is now managed by LastfmAuthenticatorService, no need to cache it here.
        Debug.WriteLine($"[ScrobblingService] Settings refreshed. Scrobbling Enabled: {IsScrobblingEnabled}, Credentials Configured: {AreCredentialsEffectivelyConfigured}, Thresholds: {_currentSettings.ScrobbleThresholdPercentage}% / {_currentSettings.ScrobbleThresholdAbsoluteSeconds}s");
    }

    private async Task<LastfmClient?> GetAuthenticatedClientAsync()
    {
        // Refresh settings before attempting to get a client to ensure AppSettings used by authenticator are current
        // This might be redundant if RefreshSettings() is called frequently elsewhere, but safe.
        // No, _authenticatorService.GetAuthenticatedClientAsync() loads its own fresh copy of settings.
        // So, RefreshSettings() here is mainly for _currentSettings used by IsScrobblingEnabled and ShouldScrobble.
        RefreshSettings();
        return await _authenticatorService.GetAuthenticatedClientAsync();
    }

    public bool ShouldScrobble(Song song, TimeSpan playedDuration)
    {
        RefreshSettings(); // Ensure _currentSettings is up-to-date for threshold values

        if (song == null || song.Duration.TotalSeconds <= MinTrackLengthForScrobbleSeconds)
        {
            Debug.WriteLine($"[ScrobblingService] ShouldScrobble: Song '{song?.Title ?? "null"}' is null or too short ({song?.Duration.TotalSeconds ?? 0}s). Min required: {MinTrackLengthForScrobbleSeconds}s. Returning false.");
            return false;
        }

        double percentagePlayed = (playedDuration.TotalSeconds / song.Duration.TotalSeconds) * 100.0;
        double requiredPlaybackFromPercentage = song.Duration.TotalSeconds * (_currentSettings.ScrobbleThresholdPercentage / 100.0);
        double requiredPlaybackAbsolute = _currentSettings.ScrobbleThresholdAbsoluteSeconds;
        double effectiveRequiredSeconds = Math.Min(requiredPlaybackFromPercentage, requiredPlaybackAbsolute);
        bool conditionMet = playedDuration.TotalSeconds >= effectiveRequiredSeconds;

        Debug.WriteLine($"[ScrobblingService] ShouldScrobble for '{song.Title}': " +
                        $"Played: {playedDuration.TotalSeconds:F1}s ({percentagePlayed:F1}%), " +
                        $"Song Duration: {song.Duration.TotalSeconds:F1}s. " +
                        $"Configured Thresholds: {_currentSettings.ScrobbleThresholdPercentage}% (gives {requiredPlaybackFromPercentage:F1}s) OR {_currentSettings.ScrobbleThresholdAbsoluteSeconds}s. " +
                        $"Effective Threshold: {effectiveRequiredSeconds:F1}s. Met: {conditionMet}");
        return conditionMet;
    }


    public async Task UpdateNowPlayingAsync(Song song)
    {
        // RefreshSettings() is called by GetAuthenticatedClientAsync() if needed,
        // but also good to call here to check IsScrobblingEnabled with latest settings.
        RefreshSettings();
        if (!IsScrobblingEnabled || song == null)
        {
            Debug.WriteLine($"[ScrobblingService] UpdateNowPlayingAsync skipped. Enabled: {IsScrobblingEnabled}, Song: {song?.Title ?? "null"}");
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
            Debug.WriteLine($"[ScrobblingService] Sending UpdateNowPlaying for: {song.Title} by {song.Artist}");
            var trackInfo = new LastTrack { Name = song.Title, ArtistName = song.Artist, AlbumName = song.Album };
            if (song.Duration.TotalSeconds > 0)
            {
                trackInfo.Duration = song.Duration;
            }

            var scrobble = new Scrobble(song.Artist, song.Album, song.Title, DateTimeOffset.Now);
            var response = await client.Track.UpdateNowPlayingAsync(scrobble);

            if (response.Success)
            {
                Debug.WriteLine($"[ScrobblingService] UpdateNowPlaying successful for: {song.Title}");
            }
            else
            {
                Debug.WriteLine($"[ScrobblingService] UpdateNowPlaying FAILED for: {song.Title}. Error: {response.Status} - {response.Error}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ScrobblingService] CRITICAL EXCEPTION during UpdateNowPlaying for {song.Title}: {ex.Message}");
        }
    }

    public async Task ScrobbleAsync(Song song, DateTime timePlayed)
    {
        RefreshSettings();
        if (!IsScrobblingEnabled || song == null)
        {
            Debug.WriteLine($"[ScrobblingService] ScrobbleAsync skipped. Enabled: {IsScrobblingEnabled}, Song: {song?.Title ?? "null"}");
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
            Debug.WriteLine($"[ScrobblingService] Sending Scrobble for: {song.Title} by {song.Artist}, TimePlayed: {timePlayed}");

            var scrobble = new Scrobble(song.Artist, song.Album, song.Title, timePlayed);
            var response = await client.Track.ScrobbleAsync(scrobble);

            if (response.Success)
            {
                Debug.WriteLine($"[ScrobblingService] Scrobble successful for: {song.Title}");
            }
            else
            {
                Debug.WriteLine($"[ScrobblingService] Scrobble FAILED for: {song.Title}. Error: {response.Status} - {response.Error}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ScrobblingService] CRITICAL EXCEPTION during Scrobble for {song.Title}: {ex.Message}");
        }
    }
}