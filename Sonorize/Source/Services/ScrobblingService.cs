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
    private readonly ScrobbleEligibilityService _eligibilityService; // New dependency
    private AppSettings _currentSettings;

    public bool IsScrobblingEnabled => _currentSettings.LastfmScrobblingEnabled;

    public bool AreCredentialsEffectivelyConfigured => _authenticatorService.AreCredentialsEffectivelyConfigured(_currentSettings);

    public ScrobblingService(
        SettingsService settingsService,
        LastfmAuthenticatorService authenticatorService,
        ScrobbleEligibilityService eligibilityService) // Added eligibilityService
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _authenticatorService = authenticatorService ?? throw new ArgumentNullException(nameof(authenticatorService));
        _eligibilityService = eligibilityService ?? throw new ArgumentNullException(nameof(eligibilityService)); // Store new dependency
        RefreshSettings(); // Initial load of settings
        Debug.WriteLine($"[ScrobblingService] Initialized. Scrobbling Enabled: {IsScrobblingEnabled}, Credentials Configured: {AreCredentialsEffectivelyConfigured}");
    }

    public void RefreshSettings()
    {
        _currentSettings = _settingsService.LoadSettings();
        Debug.WriteLine($"[ScrobblingService] Settings refreshed. Scrobbling Enabled: {IsScrobblingEnabled}, Credentials Configured: {AreCredentialsEffectivelyConfigured}, Thresholds: {_currentSettings.ScrobbleThresholdPercentage}% / {_currentSettings.ScrobbleThresholdAbsoluteSeconds}s");
    }

    private async Task<LastfmClient?> GetAuthenticatedClientAsync()
    {
        RefreshSettings();
        return await _authenticatorService.GetAuthenticatedClientAsync();
    }

    public bool ShouldScrobble(Song song, TimeSpan playedDuration)
    {
        RefreshSettings(); // Ensure _currentSettings is up-to-date for threshold values

        var thresholds = new ScrobbleThresholds(
            _currentSettings.ScrobbleThresholdPercentage,
            _currentSettings.ScrobbleThresholdAbsoluteSeconds);

        return ScrobbleEligibilityService.ShouldScrobble(song, playedDuration, thresholds);
    }


    public async Task UpdateNowPlayingAsync(Song song)
    {
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