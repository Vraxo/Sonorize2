using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
// Removed: using Avalonia.Platform.Storage; // No longer directly used here
using Sonorize.Models; // Required for AppSettings type
using Sonorize.Services;
using Sonorize.ViewModels.Settings;

namespace Sonorize.ViewModels;


public enum AppearanceSettingsViewSection
{
    LibraryList,
    GridView,
    UILayout
}


public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly SettingsPersistenceManager _settingsPersistenceManager;

    public MusicDirectoriesSettingsViewModel MusicDirectoriesSettings { get; }
    public ThemeSettingsViewModel ThemeSettings { get; }
    public AppearanceSettingsViewModel AppearanceSettings { get; }
    public LastfmSettingsViewModel LastfmSettings { get; }


    public bool SettingsChanged { get; private set; } = false;

    private SettingsViewSection _currentSettingsViewSection = SettingsViewSection.Directories;
    public SettingsViewSection CurrentSettingsViewSection
    {
        get => _currentSettingsViewSection;
        set => SetProperty(ref _currentSettingsViewSection, value);
    }

    private AppearanceSettingsViewSection _currentAppearanceSettingsViewSection = AppearanceSettingsViewSection.LibraryList;
    public AppearanceSettingsViewSection CurrentAppearanceSettingsViewSection
    {
        get => _currentAppearanceSettingsViewSection;
        set => SetProperty(ref _currentAppearanceSettingsViewSection, value);
    }

    private bool _isShowingAppearanceSubView;
    public bool IsShowingAppearanceSubView
    {
        get => _isShowingAppearanceSubView;
        set => SetProperty(ref _isShowingAppearanceSubView, value);
    }


    public ICommand SaveAndCloseCommand { get; }
    public ICommand ShowDirectoriesSettingsCommand { get; }
    public ICommand ShowThemeSettingsCommand { get; }
    public ICommand ShowAppearanceSettingsCommand { get; }
    public ICommand ShowScrobblingSettingsCommand { get; }

    public ICommand ShowMainSettingsViewCommand { get; }
    public ICommand ShowLibraryListSettingsCommand { get; }
    public ICommand ShowGridViewSettingsCommand { get; }
    public ICommand ShowUILayoutSettingsCommand { get; }


    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsPersistenceManager = new SettingsPersistenceManager(settingsService);

        var settings = _settingsService.LoadSettings();

        MusicDirectoriesSettings = new MusicDirectoriesSettingsViewModel(settings.General.MusicDirectories, MarkSettingsChanged);

        var themeServiceForChild = new ThemeService(settings.General.PreferredThemeFileName);
        ThemeSettings = new ThemeSettingsViewModel(settings.General.PreferredThemeFileName, themeServiceForChild, MarkSettingsChanged);

        AppearanceSettings = new AppearanceSettingsViewModel(settings.Appearance, MarkSettingsChanged);

        LastfmSettings = new LastfmSettingsViewModel();
        LastfmSettings.LoadFromSettings(settings.Lastfm);
        LastfmSettings.PropertyChanged += (s, e) => MarkSettingsChanged();


        SettingsChanged = false; // Initial state is unchanged

        SaveAndCloseCommand = new RelayCommand(SaveSettings);

        ShowDirectoriesSettingsCommand = new RelayCommand(_ => { CurrentSettingsViewSection = SettingsViewSection.Directories; IsShowingAppearanceSubView = false; });
        ShowThemeSettingsCommand = new RelayCommand(_ => { CurrentSettingsViewSection = SettingsViewSection.Theme; IsShowingAppearanceSubView = false; });
        ShowAppearanceSettingsCommand = new RelayCommand(_ =>
        {
            CurrentSettingsViewSection = SettingsViewSection.Appearance;
            CurrentAppearanceSettingsViewSection = AppearanceSettingsViewSection.LibraryList; // Default to first sub-section
            IsShowingAppearanceSubView = true;
        });
        ShowScrobblingSettingsCommand = new RelayCommand(_ => { CurrentSettingsViewSection = SettingsViewSection.Scrobbling; IsShowingAppearanceSubView = false; });

        ShowMainSettingsViewCommand = new RelayCommand(_ => { IsShowingAppearanceSubView = false; CurrentSettingsViewSection = SettingsViewSection.Directories; });
        ShowLibraryListSettingsCommand = new RelayCommand(_ => CurrentAppearanceSettingsViewSection = AppearanceSettingsViewSection.LibraryList);
        ShowGridViewSettingsCommand = new RelayCommand(_ => CurrentAppearanceSettingsViewSection = AppearanceSettingsViewSection.GridView);
        ShowUILayoutSettingsCommand = new RelayCommand(_ => CurrentAppearanceSettingsViewSection = AppearanceSettingsViewSection.UILayout);
    }

    private void MarkSettingsChanged()
    {
        if (SettingsChanged)
        {
            return;
        }

        SettingsChanged = true;
        Debug.WriteLine("[SettingsVM] Settings marked as changed (UI interaction or child VM).");
    }

    private void SaveSettings(object? parameter)
    {
        AppSettings settingsOnDisk = _settingsService.LoadSettings();

        bool changesPersisted = _settingsPersistenceManager.ApplyAndSaveChanges(
            settingsOnDisk,
            this.MusicDirectoriesSettings,
            this.ThemeSettings,
            this.LastfmSettings,
            this.AppearanceSettings
        );

        if (changesPersisted)
        {
            this.SettingsChanged = true;
            Debug.WriteLine("[SettingsVM] Changes were persisted by SettingsPersistenceManager.");
        }
        else
        {
            this.SettingsChanged = false;
            Debug.WriteLine("[SettingsVM] No changes were persisted by SettingsPersistenceManager.");
        }
    }
}