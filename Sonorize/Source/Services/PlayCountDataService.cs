using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Sonorize.Services;

public class PlayCountDataService
{
    private readonly string _playCountFilePath;
    private Dictionary<string, int> _playCountStore = new();
    private readonly object _lock = new();

    public PlayCountDataService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sonorizeAppDataPath = Path.Combine(appDataPath, "Sonorize");
        Directory.CreateDirectory(sonorizeAppDataPath);
        _playCountFilePath = Path.Combine(sonorizeAppDataPath, "playcounts.json");
        LoadPlayCounts();
        Debug.WriteLine($"[PlayCountDataService] Initialized. Data loaded from: {_playCountFilePath}");
    }

    private void LoadPlayCounts()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_playCountFilePath))
                {
                    var json = File.ReadAllText(_playCountFilePath);
                    _playCountStore = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
                }
                else
                {
                    _playCountStore = new();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlayCountDataService] Error loading play count data: {ex.Message}. Initializing with empty store.");
                _playCountStore = new();
            }
        }
    }

    private void SavePlayCounts()
    {
        lock (_lock)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_playCountStore, options);
                File.WriteAllText(_playCountFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlayCountDataService] Error saving play count data: {ex.Message}");
            }
        }
    }

    public int GetPlayCount(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return 0;
        lock (_lock)
        {
            _playCountStore.TryGetValue(filePath, out var count);
            return count;
        }
    }

    public void IncrementPlayCount(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        lock (_lock)
        {
            _playCountStore.TryGetValue(filePath, out var currentCount);
            _playCountStore[filePath] = currentCount + 1;
        }
        SavePlayCounts(); // Save after every increment
        Debug.WriteLine($"[PlayCountDataService] Incremented play count for \"{Path.GetFileName(filePath)}\" to {_playCountStore[filePath]}.");
    }
}