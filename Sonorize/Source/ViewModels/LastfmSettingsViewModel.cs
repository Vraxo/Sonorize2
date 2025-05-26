using Sonorize.Models; // Required for AppSettings

namespace Sonorize.ViewModels;

public class LastfmSettingsViewModel : ViewModelBase
{
    private bool _lastfmScrobblingEnabled;
    public bool LastfmScrobblingEnabled
    {
        get => _lastfmScrobblingEnabled;
        set => SetProperty(ref _lastfmScrobblingEnabled, value);
    }

    private string? _lastfmUsername;
    public string? LastfmUsername
    {
        get => _lastfmUsername;
        set => SetProperty(ref _lastfmUsername, value);
    }

    private string? _lastfmPassword;
    public string? LastfmPassword
    {
        get => _lastfmPassword;
        set => SetProperty(ref _lastfmPassword, value);
    }

    private int _scrobbleThresholdPercentage;
    public int ScrobbleThresholdPercentage
    {
        get => _scrobbleThresholdPercentage;
        set => SetProperty(ref _scrobbleThresholdPercentage, value);
    }

    private int _scrobbleThresholdAbsoluteSeconds;
    public int ScrobbleThresholdAbsoluteSeconds
    {
        get => _scrobbleThresholdAbsoluteSeconds;
        set => SetProperty(ref _scrobbleThresholdAbsoluteSeconds, value);
    }

    public LastfmSettingsViewModel() { }

    public void LoadFromSettings(AppSettings settings)
    {
        LastfmScrobblingEnabled = settings.LastfmScrobblingEnabled;
        LastfmUsername = settings.LastfmUsername;
        // Password is intentionally not re-loaded into the VM for editing by default
        // It's typically write-only from UI to settings, or used for initial auth.
        // If password editing is desired, it can be loaded here.
        // For this extraction, we'll keep it consistent with original behavior (loads LastfmPassword).
        LastfmPassword = settings.LastfmPassword;
        ScrobbleThresholdPercentage = settings.ScrobbleThresholdPercentage;
        ScrobbleThresholdAbsoluteSeconds = settings.ScrobbleThresholdAbsoluteSeconds;
    }

    public void UpdateAppSettings(AppSettings settings)
    {
        settings.LastfmScrobblingEnabled = LastfmScrobblingEnabled;
        settings.LastfmUsername = LastfmUsername;
        // Only update password if it's not null/empty, or handle more explicitly based on requirements.
        // The original SettingsViewModel directly set it, so we'll mirror that.
        if (LastfmPassword != null) // Or some other condition if password shouldn't be blanked out unintentionally
        {
            settings.LastfmPassword = LastfmPassword;
        }
        settings.ScrobbleThresholdPercentage = ScrobbleThresholdPercentage;
        settings.ScrobbleThresholdAbsoluteSeconds = ScrobbleThresholdAbsoluteSeconds;
    }
}