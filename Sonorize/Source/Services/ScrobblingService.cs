using Sonorize.Models;
using System.Diagnostics;
using System.Threading.Tasks;
using System;
using IF.Lastfm.Core.Api;
using IF.Lastfm.Core.Objects; // Added for service classes like Track, Auth

namespace Sonorize.Services;

public class ScrobblingService
{
    private readonly SettingsService _settingsService;
    private AppSettings _currentSettings;

    // !!! IMPORTANT: Replace these with your actual API key and secret from Last.fm !!!
    private const string LastfmApiKey = "d623e7a246a80c3bd60819e86c7b5ee1";
    private const string LastfmApiSecret = "9414a77c9b7f8c361d96d4575ccd97f0";
    private const int MinTrackLengthForScrobbleSeconds = 30;

    private string? _cachedSessionKey; // Cache session key for the current app session

    public bool IsScrobblingEnabled => _currentSettings.LastfmScrobblingEnabled;

    // Credentials are now configured if we have a session key, or if we have username/password to attempt to get one.
    public bool AreCredentialsEffectivelyConfigured => !string.IsNullOrEmpty(_cachedSessionKey) ||
                                                      (!string.IsNullOrEmpty(_currentSettings.LastfmUsername) &&
                                                       !string.IsNullOrEmpty(_currentSettings.LastfmPassword));

    public ScrobblingService(SettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        RefreshSettings(); // Initial load of settings and session key
        Debug.WriteLine($"[ScrobblingService] Initialized. Scrobbling Enabled: {IsScrobblingEnabled}, SessionKey Cached: {!string.IsNullOrEmpty(_cachedSessionKey)}");
    }

    public void RefreshSettings()
    {
        _currentSettings = _settingsService.LoadSettings();
        _cachedSessionKey = _currentSettings.LastfmSessionKey; // Load session key from settings
        Debug.WriteLine($"[ScrobblingService] Settings refreshed. Scrobbling Enabled: {IsScrobblingEnabled}, SessionKey Cached: {!string.IsNullOrEmpty(_cachedSessionKey)}, Thresholds: {_currentSettings.ScrobbleThresholdPercentage}% / {_currentSettings.ScrobbleThresholdAbsoluteSeconds}s");
    }

    private async Task<LastfmClient?> GetClientAsync()
    {
        if (string.IsNullOrEmpty(LastfmApiKey) || LastfmApiKey == "YOUR_LASTFM_API_KEY" ||
            string.IsNullOrEmpty(LastfmApiSecret) || LastfmApiSecret == "YOUR_LASTFM_API_SECRET")
        {
            Debug.WriteLine("[ScrobblingService] CRITICAL: Last.fm API Key or Secret is not configured. Aborting.");
            return null;
        }

        RefreshSettings(); // reload the latest settings from disk

        if (!string.IsNullOrEmpty(_cachedSessionKey))
        {
            // We already have a valid session key from a previous run.
            var auth = new LastAuth(LastfmApiKey, LastfmApiSecret);
            // Tell LastAuth to “use” the saved session key:
            auth.LoadSession(new LastUserSession { Token = _cachedSessionKey });
            return new LastfmClient(auth);
        }

        // No cached session key—attempt to authenticate with username/password
        if (!string.IsNullOrEmpty(_currentSettings.LastfmUsername) &&
            !string.IsNullOrEmpty(_currentSettings.LastfmPassword))
        {
            Debug.WriteLine($"[ScrobblingService] No session key; attempting login for '{_currentSettings.LastfmUsername}'…");
            var auth = new LastAuth(LastfmApiKey, LastfmApiSecret);

            try
            {
                var response = await auth.GetSessionTokenAsync(
                    _currentSettings.LastfmUsername,
                    _currentSettings.LastfmPassword
                );

                if (response.Success && auth.Authenticated)
                {
                    // Pull the just-obtained session out of auth.Session:
                    var session = auth.UserSession;           // UserSession
                    _cachedSessionKey = session.Token;      // The actual session key string
                    _currentSettings.LastfmSessionKey = session.Token;
                    _settingsService.SaveSettings(_currentSettings);

                    Debug.WriteLine($"[ScrobblingService] Successfully obtained session key for '{_currentSettings.LastfmUsername}'.");

                    // Return a new client that’s now “logged in” with the session key
                    var authenticatedAuth = new LastAuth(LastfmApiKey, LastfmApiSecret);
                    authenticatedAuth.LoadSession(session);
                    return new LastfmClient(authenticatedAuth);
                }
                else
                {
                    Debug.WriteLine($"[ScrobblingService] Authentication failed. " +
                                    $"Success={response.Success}, HasAuthenticated={auth.Authenticated}.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScrobblingService] Exception during Last.fm login: {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine("[ScrobblingService] Cannot login: username/password not set in settings.");
        }

        Debug.WriteLine("[ScrobblingService] GetClientAsync: Could not obtain a Last.fm client.");
        return null;
    }



    public bool ShouldScrobble(Song song, TimeSpan playedDuration)
    {
        RefreshSettings();

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
        RefreshSettings();
        if (!IsScrobblingEnabled || song == null)
        {
            Debug.WriteLine($"[ScrobblingService] UpdateNowPlayingAsync skipped. Enabled: {IsScrobblingEnabled}, Song: {song?.Title ?? "null"}");
            return;
        }

        var client = await GetClientAsync();
        if (client == null)
        {
            Debug.WriteLine("[ScrobblingService] UpdateNowPlayingAsync: No authenticated client. Skipping.");
            return;
        }

        try
        {
            Debug.WriteLine($"[ScrobblingService] Sending UpdateNowPlaying for: {song.Title} by {song.Artist}");
            var trackInfo = new LastTrack { Name = song.Title, ArtistName = song.Artist, AlbumName = song.Album };
            // Duration should be provided if known
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
        // ShouldScrobble check is done by PlaybackService before calling this.
        // We still need to check IsScrobblingEnabled and if we can get a client.
        RefreshSettings();
        if (!IsScrobblingEnabled || song == null)
        {
            Debug.WriteLine($"[ScrobblingService] ScrobbleAsync skipped. Enabled: {IsScrobblingEnabled}, Song: {song?.Title ?? "null"}");
            return;
        }

        var client = await GetClientAsync();
        if (client == null)
        {
            Debug.WriteLine("[ScrobblingService] ScrobbleAsync: No authenticated client. Skipping.");
            return;
        }

        try
        {
            Debug.WriteLine($"[ScrobblingService] Sending Scrobble for: {song.Title} by {song.Artist}, TimePlayed: {timePlayed}");

            var scrobble = new Scrobble(song.Artist, song.Album, song.Title, timePlayed);

            if (song.Duration.TotalSeconds > 0)
            {
                // While Inflatable.Lastfm ScrobbleEntry doesn't directly take duration,
                // it's good practice to have it if other libraries/APIs use it.
                // The API itself determines duration from its metadata if not provided with now playing.
            }

            var response = await client.Track.ScrobbleAsync(scrobble);

            if (response.Success)
            {
                //Debug.WriteLine($"[ScrobblingService] Scrobble successful for: {song.Title}. Accepted: {response.Scrobbles?.AcceptedCount}, Ignored: {response.Scrobbles?.IgnoredCount}");
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