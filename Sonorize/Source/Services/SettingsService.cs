using Sonorize.Models;
using System;
using System.IO;
using System.Text.Json;
using Avalonia.Controls; // For Design.IsDesignMode

namespace Sonorize.Services;

public class SettingsService
{
    private readonly string _settingsFilePath;

    public SettingsService()
    {
        if (Design.IsDesignMode)
        {
            _settingsFilePath = string.Empty; // Dummy path for design mode
            return;
        }

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sonorizeAppDataPath = Path.Combine(appDataPath, "Sonorize");
        Directory.CreateDirectory(sonorizeAppDataPath); // Ensure directory exists
        _settingsFilePath = Path.Combine(sonorizeAppDataPath, "settings.json");
    }

    public AppSettings LoadSettings()
    {
        if (Design.IsDesignMode)
        {
            return new AppSettings(); // Return default, do not touch file system
        }

        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
            // Fallback to default settings
        }
        return new AppSettings();
    }

    public void SaveSettings(AppSettings settings)
    {
        if (Design.IsDesignMode)
        {
            return; // Do nothing in design mode
        }

        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}