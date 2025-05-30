using System;
using System.Collections.ObjectModel;
using System.Linq;
using Sonorize.Services; // For ThemeService

namespace Sonorize.ViewModels;

public class ThemeSettingsViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;
    private readonly Action _notifyParentSettingsChanged;

    public ObservableCollection<string> AvailableThemes { get; } = new();

    public string? SelectedThemeFile
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            _notifyParentSettingsChanged();
            OnPropertyChanged(nameof(HasChangesFromInitialState));
        }
    }

    public string InitialSelectedThemeFile { get; }

    public bool HasChangesFromInitialState => InitialSelectedThemeFile != SelectedThemeFile;

    public ThemeSettingsViewModel(string? initialSelectedTheme, ThemeService themeService, Action notifyParentSettingsChanged)
    {
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _notifyParentSettingsChanged = notifyParentSettingsChanged ?? throw new ArgumentNullException(nameof(notifyParentSettingsChanged));

        InitialSelectedThemeFile = initialSelectedTheme ?? ThemeService.DefaultThemeFileName;

        foreach (var themeFile in _themeService.GetAvailableThemeFiles())
        {
            AvailableThemes.Add(themeFile);
        }

        SelectedThemeFile = InitialSelectedThemeFile;
        if (!AvailableThemes.Contains(SelectedThemeFile) && AvailableThemes.Any())
        {
            SelectedThemeFile = AvailableThemes.First(); // Fallback if preferred theme not found
        }
    }
}