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

    public bool IsScrobblingEnabled => _currentSettings.LastfmScrobblingEnabled;

    public bool AreCredentialsEffectivelyConfigured
    {
        get
        {
            return LastfmAuthenticatorService.AreCredentialsEffectivelyConfigured(_currentSettings);
        }
    }

    // ScrobbleEligibilityService constants and logic moved here
    private const int MinTrackLengthForScrobbleSeconds = 30;

    private static bool DetermineScrobbleEligibility(Song song, TimeSpan playedDuration, ScrobbleThresholds thresholds)
    {
        if (song == null || song.Duration.TotalSeconds <= MinTrackLengthForScrobbleSeconds)
        {
            Debug.WriteLine($"[ScrobblingService.Eligibility] Song " +
                $"'{song?.Title ?? "null"}'" +
                $" is null or too short " +
                $"({song?.Duration.TotalSeconds ?? 0}s). " +
                $"Min required: {MinTrackLengthForScrobbleSeconds}s. " +
                $"Not scrobbling.");

            return false;
        }

        double percentagePlayed = (playedDuration.TotalSeconds / song.Duration.TotalSeconds) * 100.0;
        double requiredPlaybackFromPercentage = song.Duration.TotalSeconds * (thresholds.ScrobbleThresholdPercentage / 100.0);
        double requiredPlaybackAbsolute = thresholds.ScrobbleThresholdAbsoluteSeconds;

        double effectiveRequiredSeconds = Math.Min(requiredPlaybackFromPercentage, requiredPlaybackAbsolute);

        bool conditionMet = playedDuration.TotalSeconds >= effectiveRequiredSeconds;

        Debug.WriteLine($"[ScrobblingService.Eligibility] For '{song.Title}': " +
                        $"Played: {playedDuration.TotalSeconds:F1}s ({percentagePlayed:F1}%), " +
                        $"Song Duration: {song.Duration.TotalSeconds:F1}s. " +
                        $"Configured Thresholds: {thresholds.ScrobbleThresholdPercentage}% (gives {requiredPlaybackFromPercentage:F1}s) OR {thresholds.ScrobbleThresholdAbsoluteSeconds}s. " +
                        $"Effective Threshold: {effectiveRequiredSeconds:F1}s. " +
                        $"Met: {conditionMet}");
        return conditionMet;
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
            $"Thresholds: {_currentSettings.ScrobbleThresholdPercentage}% / {_currentSettings.ScrobbleThresholdAbsoluteSeconds}s");
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
            _currentSettings.ScrobbleThresholdPercentage,
            _currentSettings.ScrobbleThresholdAbsoluteSeconds);

        return DetermineScrobbleEligibility(song, playedDuration, thresholds);
    }


    public async Task UpdateNowPlayingAsync(Song song)
    {
        // Rely on _currentSettings being up-to-date.
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
        // Rely on _currentSettings being up-to-date.
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