using Sonorize.Models; // Required for AppSettings

namespace Sonorize.ViewModels;

public class LastfmSettingsViewModel : ViewModelBase
{
    public bool LastfmScrobblingEnabled
    {
        get;

        set
        {
            SetProperty(ref field, value);
        }
    }

    public string? LastfmUsername
    {
        get;

        set
        {
            SetProperty(ref field, value);
        }
    }

    public string? LastfmPassword
    {
        get;

        set
        {
            SetProperty(ref field, value);
        }
    }

    public int ScrobbleThresholdPercentage
    {
        get;

        set
        {
            SetProperty(ref field, value);
        }
    }

    public int ScrobbleThresholdAbsoluteSeconds
    {
        get;

        set
        {
            SetProperty(ref field, value);
        }
    }

    public LastfmSettingsViewModel() { }

    public void LoadFromSettings(LastfmSettings settings)
    {
        LastfmScrobblingEnabled = settings.ScrobblingEnabled;
        LastfmUsername = settings.Username;
        LastfmPassword = settings.Password;
        ScrobbleThresholdPercentage = settings.ScrobbleThresholdPercentage;
        ScrobbleThresholdAbsoluteSeconds = settings.ScrobbleThresholdAbsoluteSeconds;
    }

    public void UpdateLastfmSettings(LastfmSettings settings)
    {
        settings.ScrobblingEnabled = LastfmScrobblingEnabled;
        settings.Username = LastfmUsername;

        if (LastfmPassword is not null)
        {
            settings.Password = LastfmPassword;
        }

        settings.ScrobbleThresholdPercentage = ScrobbleThresholdPercentage;
        settings.ScrobbleThresholdAbsoluteSeconds = ScrobbleThresholdAbsoluteSeconds;
    }
}