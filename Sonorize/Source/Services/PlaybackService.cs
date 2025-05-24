using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Sonorize.Models;
using Sonorize.ViewModels; // Required for PlaybackStateStatus enum
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SoundTouch.Net.NAudioSupport;

namespace Sonorize.Services;

// Keep enum here for completeness/clarity within the service context
public enum PlaybackStateStatus { Stopped, Playing, Paused }

public class PlaybackService : ViewModelBase, IDisposable
{
    private Song? _currentSong;
    public Song? CurrentSong
    {
        get => _currentSong;
        private set
        {
            if (SetProperty(ref _currentSong, value))
            {
                Debug.WriteLine($"[PlaybackService] CurrentSong property set to: {value?.Title ?? "null"}");
                OnPropertyChanged(nameof(HasCurrentSong));
                // When song changes, ensure state is stopped initially until Play() is called
                // CurrentPlaybackStatus = PlaybackStateStatus.Stopped; // Play() sets this correctly
                // IsPlaying = false; // Play() sets this correctly
                // CurrentPosition = TimeSpan.Zero; // InitializeNAudioPipeline sets this
                // CurrentSongDuration = TimeSpan.Zero; // InitializeNAudioPipeline sets this
                // The Play() method should handle the state transitions.
            }
        }
    }

    public bool HasCurrentSong => CurrentSong != null;

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    private PlaybackStateStatus _currentPlaybackStatus = PlaybackStateStatus.Stopped;
    public PlaybackStateStatus CurrentPlaybackStatus
    {
        get => _currentPlaybackStatus;
        private set => SetProperty(ref _currentPlaybackStatus, value);
    }

    private TimeSpan _currentPosition;
    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        private set // Changed to private set
        {
            if (SetProperty(ref _currentPosition, value))
            {
                OnPropertyChanged(nameof(CurrentPositionSeconds));
            }
        }
    }
    public double CurrentPositionSeconds => CurrentPosition.TotalSeconds;

    private TimeSpan _currentSongDuration;
    public TimeSpan CurrentSongDuration
    {
        get => _currentSongDuration;
        private set
        {
            if (SetProperty(ref _currentSongDuration, value))
            {
                OnPropertyChanged(nameof(CurrentSongDurationSeconds));
            }
        }
    }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1.0;

    private IWavePlayer? _waveOutDevice;
    private AudioFileReader? audioFileReader;
    private SmbPitchShiftingSampleProvider? pitchShifter;
    private Timer? uiUpdateTimer;
    private SoundTouchWaveProvider? soundTouch;
    private IWavePlayer? _waveOutDeviceInstanceForStopEventCheck; // Keep track of the instance to avoid handling events from old devices

    // Flag to distinguish manual stops from natural ends (end of file)
    private bool _explicitStopRequested = false;

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (Math.Abs(_playbackRate - value) > float.Epsilon)
            {
                _playbackRate = value;
                // Update the rate on the SoundTouch provider if it exists
                if (soundTouch != null) soundTouch.Tempo = _playbackRate;
                OnPropertyChanged();
            }
        }
    }

    private float _pitchSemitones = 0f;
    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            if (Math.Abs(_pitchSemitones - value) > float.Epsilon)
            {
                _pitchSemitones = value;
                // Update the pitch factor on the pitch shifter provider if it exists
                if (pitchShifter != null) pitchShifter.PitchFactor = (float)Math.Pow(2, _pitchSemitones / 12.0);
                OnPropertyChanged();
            }
        }
    }

    // Event fired when playback reaches the end of the file naturally (not stopped or paused manually)
    public event EventHandler? PlaybackEndedNaturally;


    public PlaybackService()
    {
        Debug.WriteLine("[PlaybackService] Constructor called.");
        // Initialize timer but keep it stopped
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void UpdateUiCallback(object? state)
    {
        // This callback runs on a ThreadPool thread, not the UI thread.
        // Marshal updates to the UI thread.

        // Check playback state *before* accessing audioFileReader position,
        // as accessing Position/CurrentTime might throw exceptions on some readers
        // if the device is stopped/disposed or the stream is finished.
        // Accessing _waveOutDevice?.PlaybackState is generally safer.
        if (_waveOutDevice?.PlaybackState == PlaybackState.Playing && audioFileReader != null)
        {
            // Marshal to the UI thread
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Double-check state after Dispatcher.UIThread.InvokeAsync due to potential delays
                // Ensure the audioFileReader is still valid before accessing CurrentTime
                if (_waveOutDevice?.PlaybackState != PlaybackState.Playing || audioFileReader == null || CurrentSong == null) return;

                TimeSpan currentAudioTime;
                try
                {
                    // Safely get position on the UI thread after marshaling
                    currentAudioTime = audioFileReader.CurrentTime;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PlaybackService] Error getting CurrentTime in timer callback: {ex.Message}. Stopping timer.");
                    StopUiUpdateTimer(); // Stop the timer to prevent repeated errors
                    // Let PlaybackStopped handle final cleanup and state if it fires due to this error
                    return;
                }

                // Update the ViewModel property, which will notify UI bindings
                this.CurrentPosition = currentAudioTime; // Update CurrentPosition via its private setter

                // Note: Loop region handling is within the PlaybackService itself,
                // as it directly affects seeking logic during playback.
                if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
                {
                    var loop = CurrentSong.SavedLoop;
                    // Ensure loop end is after loop start
                    // Check if current position is at or past the loop end AND not extremely close to the *total* song duration
                    // Using 200ms tolerance near the total end to allow natural end detection to take over
                    if (loop.End > loop.Start && currentAudioTime >= loop.End && currentAudioTime < audioFileReader.TotalTime - TimeSpan.FromMilliseconds(200))
                    {
                        Debug.WriteLine($"[PlaybackService] Loop active & end reached ({currentAudioTime:mm\\:ss\\.ff} >= {loop.End:mm\\:ss\\.ff}) within file ({audioFileReader.TotalTime:mm\\:ss\\.ff}). Seeking to loop start: {loop.Start:mm\\:ss\\.ff}");
                        Seek(loop.Start); // Perform the seek. Seek() handles its own logging and position update.
                    }
                    // If currentAudioTime is >= loop.End but also very close to audioFileReader.TotalTime,
                    // we let the natural end-of-file event trigger instead of looping.
                }
            });
        }
        else
        {
            // If playback state is no longer Playing, the timer should be stopped.
            // This check helps ensure the timer stops even if the PlaybackStopped event isn't handled for some reason,
            // or if the state was manually changed on the UI thread without a PlaybackStopped event (less likely).
            if (_waveOutDevice?.PlaybackState != PlaybackState.Playing)
            {
                Debug.WriteLine($"[PlaybackService] Timer callback found state is not Playing ({_waveOutDevice?.PlaybackState}). Stopping timer.");
                StopUiUpdateTimer();
            }
        }
    }

    /// <summary>
    /// Starts playback of the given song. Stops any currently playing song first.
    /// </summary>
    /// <param name="song">The song to play.</param>
    public void Play(Song song)
    {
        Debug.WriteLine($"[PlaybackService] Play requested for: {(song?.Title ?? "null song")}");
        _explicitStopRequested = false; // New playback is not a manual stop for the *previous* song ending logic

        // Stop current playback cleanly before starting a new one.
        // Pass false because we are immediately replacing the song/state.
        StopPlaybackInternal(resetCurrentSongAndRelatedState: false);

        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[PlaybackService] Play called with null/invalid/missing file song. Cleaning up and stopping.");
            // If the song is invalid, perform a full stop and reset state.
            Stop(); // Use the public Stop() for a full reset
            return;
        }

        // Set the new current song. This will notify UI and other VMs.
        CurrentSong = song;
        // Other state properties (IsPlaying, Status, Position, Duration) will be set by InitializeNAudioPipeline or Play().

        // Attempt to initialize the NAudio pipeline for the new song.
        bool pipelineInitialized = InitializeNAudioPipeline(song.FilePath);

        if (pipelineInitialized && _waveOutDevice != null && audioFileReader != null)
        {
            // If a loop is active for the new song, seek to the start of the loop before playing
            if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null && CurrentSong.SavedLoop.Start < audioFileReader.TotalTime)
            {
                Debug.WriteLine($"[PlaybackService] New song has active loop ({CurrentSong.SavedLoop.Start:mm\\:ss\\.ff} - {CurrentSong.SavedLoop.End:mm\\:ss\\.ff}). Seeking to loop start: {CurrentSong.SavedLoop.Start:mm\\:ss\\.ff} before playing.");
                // Seek handles clamping and potential tolerance.
                Seek(CurrentSong.SavedLoop.Start);
                // Note: This seek is best-effort. Playback might start slightly before or after the exact time.
            }
            // If not looping, playback starts from TimeSpan.Zero, which is already set by InitializeNAudioPipeline.

            // Start the playback device
            _waveOutDevice.Play();
            // Update state properties to reflect playing status
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            // Start the timer to update UI position
            StartUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Playback started for: {CurrentSong.Title}");
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Pipeline init failed for {Path.GetFileName(song.FilePath)}. Cleaning up and stopping.");
            // If initialization failed, perform a full stop to reset all related state cleanly.
            Stop(); // Use the public Stop() for a full reset
        }
    }

    /// <summary>
    /// Initializes the NAudio pipeline (AudioFileReader, SoundTouch, PitchShifter, WaveOutEvent).
    /// Cleans up existing resources first.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <returns>True if initialization was successful, false otherwise.</returns>
    private bool InitializeNAudioPipeline(string filePath)
    {
        Debug.WriteLine($"[PlaybackService] InitializeNAudioPipeline for: {Path.GetFileName(filePath)}");
        // Ensure any existing resources are cleaned up before creating new ones.
        // Note: StopPlaybackInternal() should have already done this when called from Play(),
        // but calling CleanUpNAudioResources() here as a safeguard ensures a clean state before Init().
        // This should happen on the UI thread if this method is called from Play() which is on the UI thread.
        CleanUpNAudioResources(); // Explicit cleanup before new initialization

        try
        {
            // 1. Create AudioFileReader
            audioFileReader = new AudioFileReader(filePath);
            Debug.WriteLine($"[PlaybackService] Loaded AudioFileReader for {Path.GetFileName(filePath)}. Channels: {audioFileReader.WaveFormat.Channels}, SampleRate: {audioFileReader.WaveFormat.SampleRate}, Duration: {audioFileReader.TotalTime}");

            // 2. Convert to SampleProvider and Mono (SoundTouch works best with Mono float samples)
            ISampleProvider sourceSampleProvider = audioFileReader.ToSampleProvider();
            // Ensure conversion to mono if needed. NAudio's ToMono() handles this.
            ISampleProvider monoSampleProvider = sourceSampleProvider.ToMono();
            // Convert back to IWaveProvider for SoundTouch
            IWaveProvider monoWaveProviderForSoundTouch = new SampleToWaveProvider(monoSampleProvider);

            // 3. Add SoundTouch for tempo/rate control
            soundTouch = new SoundTouchWaveProvider(monoWaveProviderForSoundTouch)
            {
                Tempo = PlaybackRate, // Apply current tempo setting
                Rate = 1.0f, // Rate == 1.0 doesn't change playback speed, Tempo does
                Pitch = 1.0f // Pitch == 1.0 doesn't change pitch
            };
            Debug.WriteLine($"[PlaybackService] Added SoundTouch. Output format: {soundTouch.WaveFormat}");

            // 4. Convert SoundTouch output back to ISampleProvider for Pitch Shifting
            ISampleProvider soundTouchAsSampleProvider = soundTouch.ToSampleProvider();

            // 5. Add Pitch Shifting (SMB)
            pitchShifter = new SmbPitchShiftingSampleProvider(soundTouchAsSampleProvider)
            {
                // PitchFactor is calculated from PitchSemitones. Apply current setting.
                PitchFactor = (float)Math.Pow(2, PitchSemitones / 12.0)
            };
            Debug.WriteLine($"[PlaybackService] Added PitchShifter. Output format: {pitchShifter.WaveFormat}");


            // 6. Convert final ISampleProvider output to IWaveProvider for the output device
            IWaveProvider finalWaveProviderForDevice = pitchShifter.ToWaveProvider();
            Debug.WriteLine($"[PlaybackService] Final output format for device: {finalWaveProviderForDevice.WaveFormat}");


            // 7. Initialize the output device (WaveOutEvent is suitable for desktop)
            _waveOutDevice = new WaveOutEvent();
            // Store the instance reference BEFORE attaching the handler
            _waveOutDeviceInstanceForStopEventCheck = _waveOutDevice;
            // Attach the PlaybackStopped event handler
            _waveOutDevice.PlaybackStopped += OnPlaybackStopped;

            // Initialize the device with the final wave provider
            _waveOutDevice.Init(finalWaveProviderForDevice);

            // Set song duration and initial position. Update ViewModel properties.
            CurrentSongDuration = audioFileReader.TotalTime;
            this.CurrentPosition = TimeSpan.Zero; // Position starts at the beginning for a new song

            Debug.WriteLine($"[PlaybackService] NAudio pipeline initialization COMPLETE for: {Path.GetFileName(filePath)}.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL ERROR during NAudio pipeline init for {Path.GetFileName(filePath)}: {ex.ToString()}");
            // Ensure resources are cleaned up if initialization failed at any step
            CleanUpNAudioResources();
            // Reset duration and position if init failed
            CurrentSongDuration = TimeSpan.Zero;
            this.CurrentPosition = TimeSpan.Zero;
            // The caller (Play method) will handle setting the global state to Stopped if Init fails.
            return false;
        }
    }

    /// <summary>
    /// Starts the timer used for periodically updating the UI position.
    /// </summary>
    private void StartUiUpdateTimer()
    {
        // Start or reset the timer to fire immediately and then every 100ms
        uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        // Debug.WriteLine("[PlaybackService] UI Update Timer Started.");
    }

    /// <summary>
    /// Stops the timer used for periodically updating the UI position.
    /// </summary>
    private void StopUiUpdateTimer()
    {
        // Stop the timer by setting interval to Infinite
        uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        // Debug.WriteLine("[PlaybackService] UI Update Timer Stopped.");
    }

    /// <summary>
    /// Cleans up and disposes of NAudio playback resources.
    /// This should be called whenever playback stops or before initializing a new pipeline.
    /// Assumed to be called on the UI thread or a thread safe context.
    /// </summary>
    private void CleanUpNAudioResources()
    {
        // Ensure we only detach the event from the *currently active* instance that we tracked
        if (_waveOutDevice != null && _waveOutDeviceInstanceForStopEventCheck == _waveOutDevice)
        {
            _waveOutDevice.PlaybackStopped -= OnPlaybackStopped; // Detach handler
            _waveOutDeviceInstanceForStopEventCheck = null; // Clear the tracked instance reference
        }

        // Dispose the wave output device if it exists and is not already disposed
        if (_waveOutDevice != null)
        {
            // Check state before stopping/disposing to avoid exceptions on already stopped/disposed devices
            // This check is a safeguard, as Dispose() is generally safe even if called multiple times or on a stopped device.
            if (_waveOutDevice.PlaybackState != PlaybackState.Stopped)
            {
                try { _waveOutDevice.Stop(); } // Attempt to stop if playing/paused
                catch (Exception ex) { Debug.WriteLine($"[PlaybackService] Error during _waveOutDevice.Stop() in cleanup: {ex.Message}"); }
            }
            try { _waveOutDevice.Dispose(); } // Dispose the device
            catch (Exception ex) { Debug.WriteLine($"[PlaybackService] Error during _waveOutDevice.Dispose() in cleanup: {ex.Message}"); }
            _waveOutDevice = null; // Clear the reference
        }

        // Dispose the audio file reader if it exists
        audioFileReader?.Dispose();
        audioFileReader = null; // Clear the reference

        // Nullify references to provider chain (they don't usually need explicit Dispose unless they hold significant unmanaged resources, which SoundTouchWaveProvider and SmbPitchShiftingSampleProvider don't typically)
        pitchShifter = null;
        soundTouch = null;

        Debug.WriteLine("[PlaybackService] NAudio resources cleaned up.");
    }


    /// <summary>
    /// Event handler for the PlaybackStopped event of the WaveOutEvent device.
    /// This method is responsible for determining why playback stopped (natural end, manual stop, error)
    /// and updating the service state accordingly, potentially raising the PlaybackEndedNaturally event.
    /// This handler is invoked on the UI thread due to Dispatcher.UIThread.InvokeAsync in the subscriber logic (if applicable, or explicitly marshaled).
    /// My PlaybackService.OnPlaybackStopped *is* marshaled via InvokeAsync in its implementation.
    /// </summary>
    /// <param name="sender">The source of the event (the WaveOutEvent instance).</param>
    /// <param name="e">The event arguments, including any exception if an error occurred.</param>
    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // This code runs on the UI thread because it's wrapped in Dispatcher.UIThread.InvokeAsync.

        // Check if the sender is the expected device instance to avoid handling events from old/disposed devices.
        if (sender != _waveOutDeviceInstanceForStopEventCheck)
        {
            Debug.WriteLine("[PlaybackService] OnPlaybackStopped received for a stale WaveOutDevice instance. Ignoring.");
            return;
        }
        // Safely get the last known position before cleanup
        TimeSpan lastKnownPosition = TimeSpan.Zero;
        TimeSpan? songDuration = CurrentSongDuration.TotalSeconds > 0 ? (TimeSpan?)CurrentSongDuration : null;

        try
        {
            // Try to get the position from the reader before cleanup, if it exists and is accessible
            if (audioFileReader != null)
            {
                lastKnownPosition = audioFileReader.CurrentTime;
            }
        }
        catch (Exception posEx)
        {
            Debug.WriteLine($"[PlaybackService] Error getting last known position before cleanup: {posEx.Message}");
            // Keep lastKnownPosition as TimeSpan.Zero on error
        }

        // Safely format TimeSpan? properties for logging
        TimeSpan? loopStart = CurrentSong?.SavedLoop?.Start;
        TimeSpan? loopEnd = CurrentSong?.SavedLoop?.End;
        string loopStartFormatted = loopStart.HasValue ? $"{loopStart.Value:mm\\:ss\\.ff}" : "N/A";
        string loopEndFormatted = loopEnd.HasValue ? $"{loopEnd.Value:mm\\:ss\\.ff}" : "N/A";
        string songDurationFormatted = songDuration.HasValue ? $"{songDuration.Value:mm\\:ss\\.ff}" : "N/A";


        Debug.WriteLine($"[PlaybackService] OnPlaybackStopped (UI Thread): Exception: {e.Exception?.Message ?? "None"}. ExplicitStopRequested: {_explicitStopRequested}. Current Song: {CurrentSong?.Title ?? "null"}. Current Pos Before Cleanup: {lastKnownPosition:mm\\:ss\\.ff}");
        Debug.WriteLine($"[PlaybackService] OnPlaybackStopped (UI Thread): Current Song LoopActive: {CurrentSong?.IsLoopActive ?? false}, Loop: {loopStartFormatted} - {loopEndFormatted}. Duration: {songDurationFormatted}");


        // Clean up resources immediately. This also detaches the event handler from the instance.
        CleanUpNAudioResources(); // Dispose the specific instance that fired the event

        // Stop UI timer now that playback has definitively stopped for this device instance
        StopUiUpdateTimer();

        // Check if playback stopped due to an error.
        if (e.Exception != null)
        {
            Debug.WriteLine($"[PlaybackService] Playback stopped due to error: {e.Exception.Message}");
            // Set state to stopped and reset position
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            this.CurrentPosition = TimeSpan.Zero; // Position is reset on error
            // Optionally, log the error or notify the user via a higher-level VM/UI element.
        }
        // If no exception, it was a clean stop. Now distinguish between manual stop and natural end.
        // Use the _explicitStopRequested flag to differentiate.
        else // e.Exception == null (Clean stop)
        {
            // If _explicitStopRequested is false, it means the stop was *not* explicitly requested by a user action (like clicking Stop).
            // Combined with e.Exception == null, this implies playback reached the end of the file naturally.
            // We also need to check if there *was* a CurrentSong, just in case.

            // A more robust check for natural end might be if the stop event was fired
            // and the last known position was very close to the total duration.
            bool wasNaturalEndOfFile = !_explicitStopRequested && CurrentSong != null && songDuration.HasValue && lastKnownPosition >= songDuration.Value - TimeSpan.FromMilliseconds(200); // Within 200ms of end

            // Reset the flag AFTER determining the stop reason for this event.
            // It should be reset regardless of the stop reason.
            _explicitStopRequested = false;


            if (wasNaturalEndOfFile)
            {
                Debug.WriteLine("[PlaybackService] Playback stopped naturally (event). Raising PlaybackEndedNaturally event.");
                // Reset position to the end (or duration) before raising the event,
                // or let the next song start from 0. Resetting to 0 is cleaner.
                this.CurrentPosition = TimeSpan.Zero; // Position is reset at the end of the file or on stop.
                // Fire the event. The subscriber (MainWindowViewModel) will handle logic for playing the next song or stopping.
                // The subscriber must either Play a new song or call Stop() to finalize the state.
                PlaybackEndedNaturally?.Invoke(this, EventArgs.Empty);

                // Do NOT set final state (IsPlaying/Status) here if raising the event.
                // The event handler is responsible for the subsequent state change (either Play() or Stop()).
            }
            else // It was a clean stop, but explicitly requested (e.g., user clicked Stop, or Play was called before the old song finished) or CurrentSong was null
            {
                Debug.WriteLine($"[PlaybackService] Playback stopped manually (event) or new song requested, or CurrentSong was null ({CurrentSong == null}). Finalizing state.");
                // Set state to stopped and reset position.
                // Note: If Play() called StopPlaybackInternal(false), CurrentSong/Duration are preserved.
                // If public Stop() called StopPlaybackInternal(true), CurrentSong/Duration are nulled/zeroed.
                // The state properties (IsPlaying, Status) should consistently reflect "Stopped".
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                this.CurrentPosition = TimeSpan.Zero; // Reset position on manual stop
                                                      // CurrentSong/Duration are managed by the caller of StopPlaybackInternal.
            }
        }
        Debug.WriteLine("[PlaybackService] OnPlaybackStopped handler finishes.");
    }

    /// <summary>
    /// Pauses playback.
    /// </summary>
    public void Pause()
    {
        // Do NOT set _explicitStopRequested to true for a pause.
        if (IsPlaying && _waveOutDevice != null && _waveOutDevice.PlaybackState == PlaybackState.Playing)
        {
            Debug.WriteLine("[PlaybackService] Pause requested.");
            _waveOutDevice.Pause(); // Pause the device. PlaybackStopped is NOT usually fired on pause.
            IsPlaying = false; // Update state properties
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            StopUiUpdateTimer(); // Stop the timer while paused
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Pause requested but not currently playing. State: {_waveOutDevice?.PlaybackState}. Doing nothing.");
        }
    }

    /// <summary>
    /// Resumes playback from a paused state or starts playback from the beginning if stopped.
    /// </summary>
    public void Resume()
    {
        Debug.WriteLine($"[PlaybackService] Resume requested. Current Status: {CurrentPlaybackStatus}, HasSong: {HasCurrentSong}");

        if (CurrentSong == null)
        {
            Debug.WriteLine("[PlaybackService] Resume requested but no CurrentSong is set. Cannot resume.");
            return;
        }

        // If currently paused, simply call Play() on the device.
        if (_waveOutDevice != null && _waveOutDevice.PlaybackState == PlaybackState.Paused && audioFileReader != null)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Paused state. Playing device.");
            _waveOutDevice.Play(); // Resume playback. PlaybackState changes to Playing.
            IsPlaying = true; // Update state properties
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer(); // Restart the timer
        }
        // If stopped, and there is a CurrentSong set (meaning it was stopped after playing, not initially idle),
        // we should re-initialize the pipeline and play from the last position or 0.
        // Calling Play(CurrentSong) achieves this as Play handles initialization from scratch.
        // If PlaybackStatus is Stopped but device is null/disposed, re-initialization is needed.
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Stopped state. Re-playing current song.");
            // Calling Play(CurrentSong) will stop any existing playback (which there shouldn't be),
            // initialize a new pipeline, and start playback from 0 (or loop start).
            // The state (_explicitStopRequested) is handled within Play().
            Play(CurrentSong);
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Resume requested but conditions not met. Device State: {_waveOutDevice?.PlaybackState ?? PlaybackState.Stopped}, AFR: {audioFileReader != null}, Current Playback Status: {CurrentPlaybackStatus}. Doing nothing.");
        }
    }

    /// <summary>
    /// Initiates the stopping process. Sets the _explicitStopRequested flag and calls Stop() on the device if it exists.
    /// This method relies on the PlaybackStopped event handler to perform cleanup and final state updates.
    /// </summary>
    /// <param name="resetCurrentSongAndRelatedState">If true, indicates a full stop that should also nullify CurrentSong etc. If false, indicates a stop initiated as part of starting a new song.</param>
    private void StopPlaybackInternal(bool resetCurrentSongAndRelatedState = true)
    {
        Debug.WriteLine($"[PlaybackService] StopPlaybackInternal called. Reset state: {resetCurrentSongAndRelatedState}. Current device state: {_waveOutDevice?.PlaybackState}");

        // Set the flag based on whether the song/state should be reset.
        // If resetCurrentSongAndRelatedState is true (called from public Stop()), it IS an explicit user/VM stop.
        // If resetCurrentSongAndRelatedState is false (called from Play()), it is NOT an explicit stop for the auto-advance logic.
        _explicitStopRequested = resetCurrentSongAndRelatedState;


        if (_waveOutDevice != null)
        {
            // Only call Stop() if the device is not already stopped.
            // Calling Stop() on an already stopped device can sometimes throw exceptions or be a no-op.
            if (_waveOutDevice.PlaybackState != PlaybackState.Stopped)
            {
                Debug.WriteLine("[PlaybackService] Calling device.Stop() from StopPlaybackInternal.");
                try
                {
                    _waveOutDevice.Stop(); // This should trigger the PlaybackStopped event
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PlaybackService] Error calling _waveOutDevice.Stop() in StopPlaybackInternal: {ex.Message}. Manually triggering cleanup.");
                    // If Stop() fails, the event might not fire. We need to manually trigger the cleanup/state logic.
                    // Marshal this to the UI thread as OnPlaybackStopped would be.
                    Dispatcher.UIThread.InvokeAsync(() => {
                        Debug.WriteLine("[PlaybackService] Manually triggering cleanup/state reset on UI thread after _waveOutDevice.Stop() failed.");
                        // Simulate a manual stop with no error if device.Stop() itself failed.
                        CleanUpNAudioResources(); // Clean up the specific instance that was active
                        StopUiUpdateTimer();
                        IsPlaying = false;
                        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                        this.CurrentPosition = TimeSpan.Zero; // Reset position on manual stop
                                                              // If resetCurrentSongAndRelatedState was true, CurrentSong/Duration should be nulled/zeroed.
                                                              // This is handled below *after* the manual cleanup.
                    });
                }
            }
            else
            {
                // Device is already stopped. The PlaybackStopped event won't fire.
                // We need to manually trigger the cleanup and state reset logic that OnPlaybackStopped would perform for an explicit stop.
                // This occurs when Play() is called while a device is already stopped but not fully cleaned up.
                Debug.WriteLine("[PlaybackService] StopPlaybackInternal: Device already stopped. Manually triggering cleanup/state reset for explicit stop.");
                // Marshal this to the UI thread as OnPlaybackStopped would be.
                Dispatcher.UIThread.InvokeAsync(() => {
                    Debug.WriteLine("[PlaybackService] Manually triggering cleanup/state reset on UI thread for already stopped device.");
                    CleanUpNAudioResources(); // Clean up the instance that was supposedly active
                    StopUiUpdateTimer();
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    this.CurrentPosition = TimeSpan.Zero; // Reset position on manual stop
                                                          // If resetCurrentSongAndRelatedState was true, CurrentSong/Duration should be nulled/zeroed.
                                                          // This is handled below *after* the manual cleanup.
                });
            }
        }
        else // _waveOutDevice is already null
        {
            // Playback is already fully stopped with no active device.
            // Ensure state is consistent as if a manual stop completed.
            Debug.WriteLine("[PlaybackService] StopPlaybackInternal: No active device found. Ensuring state is stopped.");
            // Manually ensure state is stopped. No cleanup needed as resources are already null.
            StopUiUpdateTimer(); // Ensure timer is stopped
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            this.CurrentPosition = TimeSpan.Zero; // Reset position
                                                  // If resetCurrentSongAndRelatedState was true, CurrentSong/Duration should be nulled/zeroed.
                                                  // This is handled below.
        }

        // This part is crucial: Reset CurrentSong and related state *after* the cleanup/event handling logic
        // has been triggered or completed for the *previous* device/song.
        // If resetCurrentSongAndRelatedState is true (called from public Stop()), nullify CurrentSong.
        // If false (called from Play()), the new song is already set as CurrentSong, so don't nullify it here.
        // This ensures CurrentSong is set correctly for the *next* action (either staying null or being the new song).
        if (resetCurrentSongAndRelatedState)
        {
            CurrentSong = null; // Setting this null also triggers HasCurrentSong change
            CurrentSongDuration = TimeSpan.Zero;
            this.CurrentPosition = TimeSpan.Zero; // Ensure position is zero when no song is current
            Debug.WriteLine("[PlaybackService] StopPlaybackInternal: CurrentSong, Duration, Position reset.");
        }
        else
        {
            // CurrentSong, Duration, Position were set during Play()
            Debug.WriteLine("[PlaybackService] StopPlaybackInternal: Resources cleaned/stop initiated. Song info preserved for new song.");
        }

        // The _explicitStopRequested flag is reset *inside* OnPlaybackStopped or the manual cleanup handler.
    }

    /// <summary>
    /// Public method to stop playback completely and reset the current song state.
    /// </summary>
    public void Stop()
    {
        Debug.WriteLine("[PlaybackService] Public Stop() called. Performing full internal stop.");
        // Call the internal stop method, requesting a full state reset (CurrentSong = null).
        // This sets _explicitStopRequested to true.
        StopPlaybackInternal(resetCurrentSongAndRelatedState: true);
    }

    /// <summary>
    /// Seeks to a specific position in the current song.
    /// Respects active loop regions by snapping the target position if needed.
    /// </summary>
    /// <param name="requestedPosition">The desired time position.</param>
    public void Seek(TimeSpan requestedPosition)
    {
        // Check if there's a song loaded and audio file reader is initialized
        if (audioFileReader == null || CurrentSong == null)
        {
            Debug.WriteLine($"[PlaybackService] Seek ignored: No active audio file reader or current song.");
            return;
        }

        // Ensure the playback device is not stopped before attempting to seek the reader.
        // This check prevents trying to seek a reader attached to a device that is already stopped/disposed,
        // which can lead to exceptions.
        if (_waveOutDevice?.PlaybackState == PlaybackState.Stopped)
        {
            Debug.WriteLine("[PlaybackService] Seek ignored: Playback device is stopped.");
            return;
        }


        TimeSpan targetPosition = requestedPosition;

        // Apply loop region constraints if an active loop is defined for the current song.
        // If seeking *into* an active loop from *outside* its start, snap to start.
        // If seeking *into* an active loop from *after* its end, snap to start (typical loop behavior).
        // If seeking *outside* an active loop while playing the looped section, current logic snaps to loop start.
        if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
        {
            var loop = CurrentSong.SavedLoop;
            // Ensure loop end is after loop start
            if (loop.End > loop.Start)
            {
                // If the target position is outside the loop's bounds [loop.Start, loop.End),
                // snap the target position to the loop's start time.
                if (targetPosition < loop.Start || targetPosition >= loop.End)
                {
                    Debug.WriteLine($"[PlaybackService] Seek: Loop active, target {targetPosition:mm\\:ss\\.ff} is outside loop [{loop.Start:mm\\:ss\\.ff}-{loop.End:mm\\:ss\\.ff}). Snapping to loop start: {loop.Start:mm\\:ss\\.ff}.");
                    targetPosition = loop.Start;
                }
                // If targetPosition is within [loop.Start, loop.End), allow normal seek within the loop.
            }
        }

        // Clamp targetPosition to a valid range within the audio file's total duration.
        // Seeking exactly to TotalTime can sometimes cause issues with readers.
        // Subtract a small margin (e.g., 100ms) from the total time to get the maximum seekable position.
        // Ensure the margin doesn't make the max seekable position negative for very short files.
        var totalMs = audioFileReader.TotalTime.TotalMilliseconds;
        var seekMarginMs = totalMs > 100 ? 100 : 0; // Subtract 100ms if total time is over 100ms
        var maxSeekablePosition = TimeSpan.FromMilliseconds(totalMs - seekMarginMs);
        // Ensure max seekable position is not less than zero.
        if (maxSeekablePosition < TimeSpan.Zero) maxSeekablePosition = TimeSpan.Zero;

        // Apply clamping to the target position
        targetPosition = TimeSpan.FromSeconds(Math.Clamp(targetPosition.TotalSeconds, 0, maxSeekablePosition.TotalSeconds));

        // Add a small tolerance check to avoid seeking if the target is very close to the current position.
        // This can reduce unnecessary operations from minor slider fluctuations and potentially prevent tight seeking loops.
        double positionToleranceSeconds = 0.2; // Increased tolerance to 200 milliseconds
        try
        {
            // Safely get current time for tolerance check
            TimeSpan currentAudioTimeForToleranceCheck = TimeSpan.Zero;
            if (audioFileReader != null)
            {
                currentAudioTimeForToleranceCheck = audioFileReader.CurrentTime;
            }

            if (Math.Abs(currentAudioTimeForToleranceCheck.TotalSeconds - targetPosition.TotalSeconds) < positionToleranceSeconds)
            {
                Debug.WriteLine($"[PlaybackService] Seek target {targetPosition:mm\\:ss\\.ff} is very close to current position {currentAudioTimeForToleranceCheck:mm\\:ss\\.ff} (within {positionToleranceSeconds}s), ignoring seek.");
                // We don't explicitly update this.CurrentPosition here; it will be updated by the timer callback if playing.
                // This prevents a seek, but doesn't prevent the timer from reading the position and potentially triggering a loop seek if past the end.
                // The loop check in UpdateUiCallback needs to be robust.
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Error checking current position for seek tolerance: {ex.Message}. Proceeding with seek.");
            // Ignore error, continue with seek
        }

        // Safely get current time for logging before seek
        string currentAudioTimeFormatted = "N/A";
        try
        {
            if (audioFileReader != null)
            {
                currentAudioTimeFormatted = $"{audioFileReader.CurrentTime:mm\\:ss\\.ff}";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Error formatting current position for log before seek: {ex.Message}");
        }

        Debug.WriteLine($"[PlaybackService] Seeking AudioFileReader to: {targetPosition:mm\\:ss\\.ff}. Current AFR Time before seek: {currentAudioTimeFormatted}");

        // Perform the actual seek operation on the AudioFileReader.
        try
        {
            // Ensure audioFileReader is still valid before setting position
            if (audioFileReader != null)
            {
                audioFileReader.CurrentTime = targetPosition; // Set the position
                                                              // Read back the position immediately after setting to get the actual position the reader moved to.
                this.CurrentPosition = audioFileReader.CurrentTime; // Update ViewModel property
                Debug.WriteLine($"[PlaybackService] Seek executed. AFR Time after seek: {audioFileReader.CurrentTime:mm\\:ss\\.ff}. VM Position: {this.CurrentPosition:mm\\:ss\\.ff}");
            }
            else
            {
                Debug.WriteLine($"[PlaybackService] Seek failed: audioFileReader is null.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Error during Seek to {targetPosition:mm\\:ss\\.ff}: {ex.Message}");
            // Update VM position to reflect the last known good position, or zero on error
            // If audioFileReader is still valid, try to read current position after failed seek attempt.
            if (audioFileReader != null)
            {
                try { this.CurrentPosition = audioFileReader.CurrentTime; }
                catch (Exception readEx) { Debug.WriteLine($"[PlaybackService] Error reading position after failed seek: {readEx.Message}"); }
            }
        }
    }

    /// <summary>
    /// Disposes of managed and unmanaged resources used by the playback service.
    /// This should be called when the service is no longer needed (e.g., application shutdown).
    /// </summary>
    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose() called.");

        // Ensure timer is stopped and disposed
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;

        // Clean up NAudio resources. This includes stopping the device and disposing the reader/device.
        // This also detaches the PlaybackStopped event handler.
        CleanUpNAudioResources();

        // Ensure the explicit stop flag is false
        _explicitStopRequested = false;

        // Explicitly clear state properties for completeness, although the service object itself is being disposed.
        CurrentSong = null;
        CurrentSongDuration = TimeSpan.Zero;
        this.CurrentPosition = TimeSpan.Zero;
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;

        // No other disposable resources managed directly by PlaybackService currently.

        // Suppress finalization as Dispose has cleaned up resources.
        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackService] Dispose() completed.");
    }

    // Finalizer (destructor) as a safeguard to ensure Dispose is called if the object is not explicitly disposed.
    // Useful for unmanaged resources like audio devices, though our CleanUpNAudioResources handles the key parts.
    ~PlaybackService()
    {
        Debug.WriteLine("[PlaybackService] Finalizer called for PlaybackService.");
        // Call the public Dispose method.
        Dispose(); // Call the public Dispose method to perform cleanup
        Debug.WriteLine("[PlaybackService] Finalizer completed for PlaybackService.");
    }
}