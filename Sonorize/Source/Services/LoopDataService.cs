// Path: Source/Services/LoopDataService.cs
using Sonorize.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace Sonorize.Services;

public class LoopDataService
{
    private readonly string _loopDataFilePath;
    private Dictionary<string, LoopStorageData> _loopDataStore = new();
    private readonly object _lock = new object(); // For thread safety during save/load

    public LoopDataService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sonorizeAppDataPath = Path.Combine(appDataPath, "Sonorize");
        Directory.CreateDirectory(sonorizeAppDataPath); // Ensure directory exists
        _loopDataFilePath = Path.Combine(sonorizeAppDataPath, "loopdata.json");
        LoadLoopData();
        Debug.WriteLine($"[LoopDataService] Initialized. Data loaded from: {_loopDataFilePath}");
    }

    private void LoadLoopData()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_loopDataFilePath))
                {
                    var json = File.ReadAllText(_loopDataFilePath);
                    _loopDataStore = JsonSerializer.Deserialize<Dictionary<string, LoopStorageData>>(json) ?? new Dictionary<string, LoopStorageData>();
                    Debug.WriteLine($"[LoopDataService] Successfully loaded {_loopDataStore.Count} loop entries.");
                }
                else
                {
                    _loopDataStore = new Dictionary<string, LoopStorageData>();
                    Debug.WriteLine($"[LoopDataService] Loop data file not found. Initialized with empty store.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoopDataService] Error loading loop data: {ex.Message}. Initializing with empty store.");
                _loopDataStore = new Dictionary<string, LoopStorageData>();
            }
        }
    }

    private void SaveLoopData()
    {
        lock (_lock)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_loopDataStore, options);
                File.WriteAllText(_loopDataFilePath, json);
                Debug.WriteLine($"[LoopDataService] Successfully saved {_loopDataStore.Count} loop entries to {_loopDataFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoopDataService] Error saving loop data: {ex.Message}");
            }
        }
    }

    public LoopStorageData? GetLoop(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;
        lock (_lock)
        {
            _loopDataStore.TryGetValue(filePath, out var loopData);
            if (loopData != null)
            {
                // Debug.WriteLine($"[LoopDataService] GetLoop for \"{Path.GetFileName(filePath)}\": Found Start={loopData.Start}, End={loopData.End}");
            }
            return loopData;
        }
    }

    public void SetLoop(string filePath, TimeSpan start, TimeSpan end)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        var loopData = new LoopStorageData(start, end);
        lock (_lock)
        {
            _loopDataStore[filePath] = loopData;
        }
        Debug.WriteLine($"[LoopDataService] SetLoop for \"{Path.GetFileName(filePath)}\": Start={start}, End={end}. Triggering save.");
        SaveLoopData(); // Save changes immediately
    }

    public void ClearLoop(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        bool removed = false;
        lock (_lock)
        {
            if (_loopDataStore.ContainsKey(filePath))
            {
                removed = _loopDataStore.Remove(filePath);
            }
        }
        if (removed)
        {
            Debug.WriteLine($"[LoopDataService] ClearLoop for \"{Path.GetFileName(filePath)}\". Triggering save.");
            SaveLoopData(); // Save changes immediately
        }
        else
        {
            Debug.WriteLine($"[LoopDataService] ClearLoop for \"{Path.GetFileName(filePath)}\": No loop found to clear.");
        }
    }
}