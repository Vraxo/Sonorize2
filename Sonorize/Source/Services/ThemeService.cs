using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Sonorize.Models;

namespace Sonorize.Services;

public class ThemeService
{
    private readonly string _themesDirectory;
    public ThemeColors CurrentTheme { get; private set; }

    public const string DefaultThemeFileName = "DefaultTheme.json"; // Made public const
    private const string AmoledSpotifyThemeFileName = "AmoledSpotify.json";


    public ThemeService(string? preferredThemeNameFromSettings)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var dataDirectory = Path.Combine(baseDirectory, "Data");
        _themesDirectory = Path.Combine(dataDirectory, "Themes");
        Directory.CreateDirectory(_themesDirectory);

        EnsureDefaultThemesExist();

        string themeToLoad = DefaultThemeFileName; // Default to this one first

        if (!string.IsNullOrEmpty(preferredThemeNameFromSettings))
        {
            // Check if preferred theme exists
            if (File.Exists(Path.Combine(_themesDirectory, preferredThemeNameFromSettings)))
            {
                themeToLoad = preferredThemeNameFromSettings;
            }
            else
            {
                Debug.WriteLine($"[ThemeService] Preferred theme '{preferredThemeNameFromSettings}' not found. Falling back to default.");
            }
        }

        CurrentTheme = LoadThemeFromFile(themeToLoad);
        if (CurrentTheme == null) // If chosen (or default) theme failed, use hardcoded
        {
            Debug.WriteLine($"[ThemeService] Theme '{themeToLoad}' failed to load. Using hardcoded fallback (standard dark).");
            CurrentTheme = new ThemeColors(); // Hardcoded fallback (standard dark)
        }
        Debug.WriteLine($"[ThemeService] Current theme loaded: {themeToLoad} (BG: {CurrentTheme.BackgroundColor}, Accent: {CurrentTheme.AccentColor})");
    }

    private void EnsureDefaultThemesExist()
    {
        string defaultThemePath = Path.Combine(_themesDirectory, DefaultThemeFileName);
        if (!File.Exists(defaultThemePath))
        {
            // Pass a new instance with default values, including new metrics
            SaveThemeToFile(new ThemeColors(), DefaultThemeFileName);
        }

        string amoledSpotifyThemePath = Path.Combine(_themesDirectory, AmoledSpotifyThemeFileName);
        if (!File.Exists(amoledSpotifyThemePath))
        {
            SaveThemeToFile(ThemeColors.CreateAmoledSpotifyTheme(), AmoledSpotifyThemeFileName);
        }
    }

    public ThemeColors? LoadThemeFromFile(string themeFileName)
    {
        // ... (LoadThemeFromFile remains the same as previous correct version) ...
        string filePath = Path.Combine(_themesDirectory, themeFileName);
        Debug.WriteLine($"[ThemeService] Attempting to load theme from: {filePath}");
        if (File.Exists(filePath))
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var theme = JsonSerializer.Deserialize<ThemeColors>(json);
                if (theme is not null)
                {
                    Debug.WriteLine($"[ThemeService] Theme '{themeFileName}' loaded successfully.");
                    return theme;
                }
                Debug.WriteLine($"[ThemeService] Failed to deserialize theme '{themeFileName}'. Json content was: {json.Substring(0, Math.Min(json.Length, 200))}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ThemeService] Error loading theme '{themeFileName}': {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine($"[ThemeService] Theme file not found: {filePath}");
        }
        return null;
    }

    public void SaveThemeToFile(ThemeColors theme, string themeFileName)
    {
        // ... (SaveThemeToFile remains the same as previous correct version) ...
        string filePath = Path.Combine(_themesDirectory, themeFileName);
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(theme, options);
            File.WriteAllText(filePath, json);
            Debug.WriteLine($"[ThemeService] Theme '{themeFileName}' saved to: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ThemeService] Error saving theme '{themeFileName}': {ex.Message}");
        }
    }

    public List<string> GetAvailableThemeFiles()
    {
        if (!Directory.Exists(_themesDirectory))
        {
            return new List<string>();
        }
        return Directory.GetFiles(_themesDirectory, "*.json")
                        .Select(Path.GetFileName)
                        .Where(f => f is not null) // Path.GetFileName can return null
                        .ToList()!; // Non-null asserted as we filter nulls
    }
}
