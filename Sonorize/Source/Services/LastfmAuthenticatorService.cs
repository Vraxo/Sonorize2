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

    public async Task<LastfmClient?> GetAuthenticatedClientAsync()
    {
        if (string.IsNullOrEmpty(LastfmApiKey) || LastfmApiKey == "YOUR_LASTFM_API_KEY" ||
            string.IsNullOrEmpty(LastfmApiSecret) || LastfmApiSecret == "YOUR_LASTFM_API_SECRET")
        {
            Debug.WriteLine("[LastfmAuthenticatorService] CRITICAL: Last.fm API Key or Secret is not configured. Aborting.");
            return null;
        }

        AppSettings currentSettings = _settingsService.LoadSettings(); // Always load fresh settings

        // Attempt to use existing session key from settings
        if (!string.IsNullOrEmpty(currentSettings.LastfmSessionKey))
        {
            Debug.WriteLine("[LastfmAuthenticatorService] Using existing session key from settings.");
            var auth = new LastAuth(LastfmApiKey, LastfmApiSecret);
            auth.LoadSession(new LastUserSession { Token = currentSettings.LastfmSessionKey });
            return new LastfmClient(auth);
        }

        // No session key, attempt username/password authentication
        if (!string.IsNullOrEmpty(currentSettings.LastfmUsername) &&
            !string.IsNullOrEmpty(currentSettings.LastfmPassword))
        {
            Debug.WriteLine($"[LastfmAuthenticatorService] No session key; attempting login for '{currentSettings.LastfmUsername}'…");
            var auth = new LastAuth(LastfmApiKey, LastfmApiSecret);

            try
            {
                var response = await auth.GetSessionTokenAsync(
                    currentSettings.LastfmUsername,
                    currentSettings.LastfmPassword
                );

                if (response.Success && auth.Authenticated && auth.UserSession != null)
                {
                    var session = auth.UserSession;
                    currentSettings.LastfmSessionKey = session.Token;
                    // Clear the password after successful session key retrieval for security
                    currentSettings.LastfmPassword = null;
                    _settingsService.SaveSettings(currentSettings);

                    Debug.WriteLine($"[LastfmAuthenticatorService] Successfully obtained and saved session key for '{currentSettings.LastfmUsername}'. Password cleared from settings.");

                    // Return a new client instance authenticated with the new session
                    return new LastfmClient(auth); // auth object now contains the session
                }
                else
                {
                    //string errorMessage = response.Exception?.Message ?? response.Status.ToString();
                    //Debug.WriteLine($"[LastfmAuthenticatorService] Authentication failed. Success={response.Success}, Authenticated={auth.Authenticated}, Error='{errorMessage}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LastfmAuthenticatorService] Exception during Last.fm login: {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine("[LastfmAuthenticatorService] Cannot login: username/password not set in settings, and no existing session key.");
        }

        Debug.WriteLine("[LastfmAuthenticatorService] GetAuthenticatedClientAsync: Could not obtain an authenticated Last.fm client.");
        return null;
    }

    public bool AreCredentialsEffectivelyConfigured(AppSettings settings)
    {
        // Credentials are configured if we have a session key, 
        // OR if we have username/password to attempt to get one.
        return !string.IsNullOrEmpty(settings.LastfmSessionKey) ||
               (!string.IsNullOrEmpty(settings.LastfmUsername) && !string.IsNullOrEmpty(settings.LastfmPassword));
    }
}