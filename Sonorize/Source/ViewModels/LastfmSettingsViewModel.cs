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

    public void LoadFromSettings(AppSettings settings)
    {
        LastfmScrobblingEnabled = settings.LastfmScrobblingEnabled;
        LastfmUsername = settings.LastfmUsername;
        LastfmPassword = settings.LastfmPassword;
        ScrobbleThresholdPercentage = settings.ScrobbleThresholdPercentage;
        ScrobbleThresholdAbsoluteSeconds = settings.ScrobbleThresholdAbsoluteSeconds;
    }

    public void UpdateAppSettings(AppSettings settings)
    {
        settings.LastfmScrobblingEnabled = LastfmScrobblingEnabled;
        settings.LastfmUsername = LastfmUsername;
        
        if (LastfmPassword is not null)
        {
            settings.LastfmPassword = LastfmPassword;
        }
        
        settings.ScrobbleThresholdPercentage = ScrobbleThresholdPercentage;
        settings.ScrobbleThresholdAbsoluteSeconds = ScrobbleThresholdAbsoluteSeconds;
    }
}