using System;
using System.Diagnostics;
using System.IO; // Required for Path.GetFileName
using System.Text.Json;
using Sonorize.Models;

namespace Sonorize.Services;

public class LoopDataMigrator
{
    public static bool TryProcessEntry(string filePathKey, JsonElement jsonData, out LoopStorageData? loopData)
    {
        loopData = null;
        try
        {
            if (TryDeserializeCurrentFormat(jsonData, out loopData))
            {
                return true;
            }

            // If current format deserialization fails (or returns null and JsonException wasn't thrown for some reason, though unlikely for Deserialize<T>),
            // it will fall through to the catch block for JsonException if that's the cause,
            // or proceed to old format migration if Deserialize<T> just returned null without exception.
        }
        catch (JsonException)
        {
            // Deserialization to LoopStorageData failed, explicitly try migrating from old format
            if (TryMigrateOldFormat(filePathKey, jsonData, out loopData))
            {
                return true;
            }
        }
        catch (Exception exGeneral)
        {
            Debug.WriteLine($"[LoopDataMigrator] General error processing loop entry for {Path.GetFileName(filePathKey)}: {exGeneral.Message}");
            return false;
        }

        // If loopData is still null here, it means it couldn't be processed as new or old format properly
        // or an unhandled scenario where Deserialize<T> returned null without throwing JsonException.
        if (loopData == null)
        {
            Debug.WriteLine($"[LoopDataMigrator] Could not deserialize or migrate loop entry for {Path.GetFileName(filePathKey)} into a known format.");
        }
        return loopData != null;
    }

    private static bool TryDeserializeCurrentFormat(JsonElement jsonData, out LoopStorageData? loopData)
    {
        // This method specifically attempts deserialization to the current format.
        // It's expected to throw JsonException if the structure doesn't match,
        // which will be caught by the caller (TryProcessEntry).
        // If Deserialize<T> can return null without an exception for certain inputs,
        // that case would also result in `loopData` being null.
        loopData = jsonData.Deserialize<LoopStorageData>();
        if (loopData != null)
        {
            // Successfully deserialized to current format
            return true;
        }
        return false; // Indicates null was returned without exception, or that current format failed.
    }

    private static bool TryMigrateOldFormat(string filePathKey, JsonElement jsonData, out LoopStorageData? loopData)
    {
        loopData = null;
        try
        {
            if (jsonData.TryGetProperty("Start", out JsonElement startElement) &&
                jsonData.TryGetProperty("End", out JsonElement endElement))
            {
                TimeSpan start = JsonSerializer.Deserialize<TimeSpan>(startElement.GetRawText());
                TimeSpan end = JsonSerializer.Deserialize<TimeSpan>(endElement.GetRawText());

                // Check if "IsActive" property exists, if not, it's the old format
                if (!jsonData.TryGetProperty("IsActive", out _))
                {
                    loopData = new LoopStorageData(start, end, false); // Default IsActive to false for old data
                    Debug.WriteLine($"[LoopDataMigrator] Migrated old loop format for {Path.GetFileName(filePathKey)} to Start={loopData.Start}, End={loopData.End}, IsActive={loopData.IsActive}");
                    return true;
                }
                else
                {
                    // This case implies "Start", "End", and "IsActive" exist, which should have been caught by TryDeserializeCurrentFormat.
                    // However, if TryDeserializeCurrentFormat failed for some other reason (e.g. type mismatch on IsActive),
                    // this path might be hit. We treat it as "not the old format we're trying to migrate from here".
                    Debug.WriteLine($"[LoopDataMigrator] Data for {Path.GetFileName(filePathKey)} has Start, End, and IsActive but failed current format deserialization. Not an old format target for this method.");
                    return false;
                }
            }
            else
            {
                Debug.WriteLine($"[LoopDataMigrator] Old format migration for {Path.GetFileName(filePathKey)} failed: 'Start' or 'End' property not found.");
            }
        }
        catch (JsonException jsonEx)
        {
            Debug.WriteLine($"[LoopDataMigrator] JSON error during old format migration for {Path.GetFileName(filePathKey)}: {jsonEx.Message}");
        }
        catch (Exception exMigrate)
        {
            Debug.WriteLine($"[LoopDataMigrator] Failed to migrate loop for {Path.GetFileName(filePathKey)} from old format: {exMigrate.Message}");
        }
        return false;
    }
}