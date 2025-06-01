using System;
using System.Diagnostics;
using System.Threading.Tasks;
using IF.Lastfm.Core.Api;
using IF.Lastfm.Core.Objects; // For LastUserSession
using Sonorize.Models;

namespace Sonorize.Services;

public class LastfmAuthenticatorService
{
    private readonly SettingsService _settingsService;

    // Last.fm API Credentials
    private const string LastfmApiKey = "d623e7a246a80c3bd60819e86c7b5ee1"; // Keep your actual key
    private const string LastfmApiSecret = "9414a77c9b7f8c361d96d4575ccd97f0"; // Keep your actual secret

    public LastfmAuthenticatorService(SettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        Debug.WriteLine("[LastfmAuthenticatorService] Initialized.");
    }

    private async Task<LastfmClient?> TryAuthenticateWithCredentialsAsync(AppSettings settings)
    {
        Debug.WriteLine($"[LastfmAuthenticatorService] Attempting credential authentication for '{settings.LastfmUsername}'…");
        var auth = new LastAuth(LastfmApiKey, LastfmApiSecret);
        try
        {
            // Callers ensure LastfmUsername and LastfmPassword are not null/empty
            var response = await auth.GetSessionTokenAsync(settings.LastfmUsername!, settings.LastfmPassword!);
            if (response.Success && auth.Authenticated && auth.UserSession != null)
            {
                var session = auth.UserSession;
                settings.LastfmSessionKey = session.Token;
                settings.LastfmPassword = null; // Clear password for security
                _settingsService.SaveSettings(settings); // Save updated settings with session key

                Debug.WriteLine($"[LastfmAuthenticatorService] Successfully obtained and saved session key for '{settings.LastfmUsername}'. Password cleared from settings.");
                return new LastfmClient(auth);
            }
            // else: If authentication failed but no exception, falls through to return null.
            // Debug.WriteLine($"[LastfmAuthenticatorService] Credential authentication failed. Success={response.Success}, Authenticated={auth.Authenticated}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LastfmAuthenticatorService] Exception during credential authentication for '{settings.LastfmUsername}': {ex.Message}");
        }
        return null;
    }

    public async Task<LastfmClient?> GetAuthenticatedClientAsync()
    {
        if (string.IsNullOrEmpty(LastfmApiKey) || LastfmApiKey == "YOUR_LASTFM_API_KEY" ||
            string.IsNullOrEmpty(LastfmApiSecret) || LastfmApiSecret == "YOUR_LASTFM_API_SECRET")
        {
            Debug.WriteLine("[LastfmAuthenticatorService] CRITICAL: Last.fm API Key or Secret is not configured. Aborting.");
            return null;
        }

        AppSettings currentSettings = _settingsService.LoadSettings(); // Always load fresh settings

        // Attempt 1: Use existing session key from settings
        if (!string.IsNullOrEmpty(currentSettings.LastfmSessionKey))
        {
            Debug.WriteLine("[LastfmAuthenticatorService] Using existing session key from settings.");
            var auth = new LastAuth(LastfmApiKey, LastfmApiSecret);
            auth.LoadSession(new LastUserSession { Token = currentSettings.LastfmSessionKey });
            return new LastfmClient(auth);
        }

        // Attempt 2: Authenticate with username/password
        if (!string.IsNullOrEmpty(currentSettings.LastfmUsername) &&
            !string.IsNullOrEmpty(currentSettings.LastfmPassword))
        {
            var client = await TryAuthenticateWithCredentialsAsync(currentSettings);
            if (client != null)
            {
                return client;
            }
            // If TryAuthenticateWithCredentialsAsync returns null, fall through
        }
        else
        {
            Debug.WriteLine("[LastfmAuthenticatorService] No session key, and username/password not fully provided in settings. Cannot attempt login.");
            // Fall through to final debug message and return null
        }

        Debug.WriteLine("[LastfmAuthenticatorService] GetAuthenticatedClientAsync: Could not obtain an authenticated Last.fm client after all attempts.");
        return null;
    }

    public static bool AreCredentialsEffectivelyConfigured(AppSettings settings)
    {
        return !string.IsNullOrEmpty(settings.LastfmSessionKey) ||
               (!string.IsNullOrEmpty(settings.LastfmUsername) && !string.IsNullOrEmpty(settings.LastfmPassword));
    }
}