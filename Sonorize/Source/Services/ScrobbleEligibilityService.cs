using System;
using System.Diagnostics;
using Sonorize.Models; // For Song

namespace Sonorize.Services;

public record ScrobbleThresholds(int ScrobbleThresholdPercentage, int ScrobbleThresholdAbsoluteSeconds);

public class ScrobbleEligibilityService
{
    private const int MinTrackLengthForScrobbleSeconds = 30;

    public static bool ShouldScrobble(Song song, TimeSpan playedDuration, ScrobbleThresholds thresholds)
    {
        if (song == null || song.Duration.TotalSeconds <= MinTrackLengthForScrobbleSeconds)
        {
            Debug.WriteLine($"[ScrobbleEligibilityService] ShouldScrobble: Song '{song?.Title ?? "null"}' is null or too short ({song?.Duration.TotalSeconds ?? 0}s). Min required: {MinTrackLengthForScrobbleSeconds}s. Returning false.");
            return false;
        }

        double percentagePlayed = (playedDuration.TotalSeconds / song.Duration.TotalSeconds) * 100.0;
        double requiredPlaybackFromPercentage = song.Duration.TotalSeconds * (thresholds.ScrobbleThresholdPercentage / 100.0);
        double requiredPlaybackAbsolute = thresholds.ScrobbleThresholdAbsoluteSeconds;

        // Effective threshold is the stricter of the two: percentage-based OR absolute time.
        // However, the Last.fm guideline is "The track must be played for at least half its duration, or for 4 minutes (whichever is shorter)."
        // This implies a MINIMUM of the two conditions (percentage vs absolute) for the *required playback time*, not the maximum.
        // E.g., if 50% is 30s, and absolute is 240s, need 30s. If 50% is 300s, and absolute is 240s, need 240s.
        double effectiveRequiredSeconds = Math.Min(requiredPlaybackFromPercentage, requiredPlaybackAbsolute);

        bool conditionMet = playedDuration.TotalSeconds >= effectiveRequiredSeconds;

        Debug.WriteLine($"[ScrobbleEligibilityService] ShouldScrobble for '{song.Title}': " +
                        $"Played: {playedDuration.TotalSeconds:F1}s ({percentagePlayed:F1}%), " +
                        $"Song Duration: {song.Duration.TotalSeconds:F1}s. " +
                        $"Configured Thresholds: {thresholds.ScrobbleThresholdPercentage}% (gives {requiredPlaybackFromPercentage:F1}s) OR {thresholds.ScrobbleThresholdAbsoluteSeconds}s. " +
                        $"Effective Threshold: {effectiveRequiredSeconds:F1}s. Met: {conditionMet}");
        return conditionMet;
    }
}