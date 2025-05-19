// Path: Source/Services/LoopDataService.cs
using Sonorize.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using Avalonia.Controls; // For Design.IsDesignMode

namespace Sonorize.Services;

public class LoopDataService
{
    private readonly string _loopDataFilePath;
    private Dictionary<string, LoopStorageData> _loopDataStore = new();
    private readonly object _lock = new object();

    public LoopDataService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var sonorizeAppDataPath = Path.Combine(appDataPath, "Sonorize");
        if (!Design.IsDesignMode) // Only create directory if not in design mode
        {
            Directory.CreateDirectory(sonorizeAppDataPath);
        }
        _loopDataFilePath = Path.Combine(sonorizeAppDataPath, "loopdata.json");
        LoadLoopData();
        Debug.WriteLine($"[LoopDataService] Initialized. Data loaded from: {_loopDataFilePath}");
    }

    private void LoadLoopData()
    {
        if (Design.IsDesignMode)
        {
            Debug.WriteLine("[LoopDataService] Design Mode: Skipping file system access for loop data loading.");
            _loopDataStore = new Dictionary<string, LoopStorageData>();
            return;
        }

        lock (_lock)
        {
            try
            {
                if (File.Exists(_loopDataFilePath))
                {
                    var json = File.ReadAllText(_loopDataFilePath);
                    // Handle potential old format without IsActive gracefully
                    var tempStore = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    _loopDataStore = new Dictionary<string, LoopStorageData>();
                    if (tempStore != null)
                    {
                        foreach (var kvp in tempStore)
                        {
                            try
                            {
                                // Try to deserialize to the new record type
                                _loopDataStore[kvp.Key] = kvp.Value.Deserialize<LoopStorageData>()!;
                            }
                            catch (JsonException) // If it fails, it might be the old format
                            {
                                try
                                {
                                    // Old format: record LoopStorageData(TimeSpan Start, TimeSpan End);
                                    var oldLoop = kvp.Value.Deserialize<OldLoopStorageDataTemp>();
                                    if (oldLoop != null)
                                    {
                                        _loopDataStore[kvp.Key] = new LoopStorageData(oldLoop.Start, oldLoop.End, false); // Default IsActive to false for old data
                                        Debug.WriteLine($"[LoopDataService] Migrated old loop format for {Path.GetFileName(kvp.Key)}");
                                    }
                                }
                                catch (Exception exMigrate)
                                {
                                    Debug.WriteLine($"[LoopDataService] Failed to migrate or deserialize loop for {Path.GetFileName(kvp.Key)}: {exMigrate.Message}");
                                }
                            }
                        }
                    }
                    Debug.WriteLine($"[LoopDataService] Successfully loaded/migrated {_loopDataStore.Count} loop entries.");
                }
                else
                {
                    _loopDataStore = new Dictionary<string, LoopStorageData>();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoopDataService] Error loading loop data: {ex.Message}. Initializing with empty store.");
                _loopDataStore = new Dictionary<string, LoopStorageData>();
            }
        }
    }
    // Temporary record for migration from old format
    private record OldLoopStorageDataTemp(TimeSpan Start, TimeSpan End);


    private void SaveLoopData()
    {
        if (Design.IsDesignMode)
        {
            Debug.WriteLine("[LoopDataService] Design Mode: Skipping file system access for loop data saving.");
            return;
        }

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