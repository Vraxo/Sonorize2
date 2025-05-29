// Path: Source/Services/LoopDataService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Sonorize.Models;

namespace Sonorize.Services;

public class LoopDataService
{
    private readonly string _loopDataFilePath;
    private Dictionary<string, LoopStorageData> _loopDataStore = new();
    private readonly object _lock = new object();
    private readonly LoopDataMigrator _loopDataMigrator;

    public LoopDataService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sonorizeAppDataPath = Path.Combine(appDataPath, "Sonorize");
        Directory.CreateDirectory(sonorizeAppDataPath);
        _loopDataFilePath = Path.Combine(sonorizeAppDataPath, "loopdata.json");
        _loopDataMigrator = new LoopDataMigrator(); // Instantiate the migrator
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
                    var tempStore = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    _loopDataStore = new Dictionary<string, LoopStorageData>();

                    if (tempStore != null)
                    {
                        foreach (var kvp in tempStore)
                        {
                            if (LoopDataMigrator.TryProcessEntry(kvp.Key, kvp.Value, out LoopStorageData? processedData) && processedData != null)
                            {
                                _loopDataStore[kvp.Key] = processedData;
                            }
                            else
                            {
                                Debug.WriteLine($"[LoopDataService] Failed to process or migrate loop entry for {Path.GetFileName(kvp.Key)}, entry skipped.");
                            }
                        }
                        Debug.WriteLine($"[LoopDataService] Successfully loaded/migrated {_loopDataStore.Count} loop entries using LoopDataMigrator.");
                    }
                    else
                    {
                        _loopDataStore = new Dictionary<string, LoopStorageData>();
                        Debug.WriteLine($"[LoopDataService] Loop data file was empty or malformed (tempStore is null).");
                    }
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

    // OldLoopStorageDataTemp record is removed from here, as it's now in LoopDataMigrator


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
            return loopData;
        }
    }

    // Modified to accept isActive
    public void SetLoop(string filePath, TimeSpan start, TimeSpan end, bool isActive)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        var loopData = new LoopStorageData(start, end, isActive);
        lock (_lock)
        {
            _loopDataStore[filePath] = loopData;
        }
        Debug.WriteLine($"[LoopDataService] SetLoop for \"{Path.GetFileName(filePath)}\": Start={start}, End={end}, IsActive={isActive}. Triggering save.");
        SaveLoopData();
    }

    public void UpdateLoopActiveState(string filePath, bool isActive)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        lock (_lock)
        {
            if (_loopDataStore.TryGetValue(filePath, out var existingLoop))
            {
                _loopDataStore[filePath] = existingLoop with { IsActive = isActive }; // Using record 'with' expression
                Debug.WriteLine($"[LoopDataService] UpdateLoopActiveState for \"{Path.GetFileName(filePath)}\" to IsActive={isActive}. Triggering save.");
                SaveLoopData();
            }
            else
            {
                Debug.WriteLine($"[LoopDataService] UpdateLoopActiveState: No loop definition found for \"{Path.GetFileName(filePath)}\" to update active state.");
            }
        }
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
            SaveLoopData();
        }
    }
}