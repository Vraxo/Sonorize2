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
using Avalonia.Animation;

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

    // Flag to signal if the stop was initiated by the public Stop() method.
    // This flag is ONLY set to true by public Stop() and reset to false in OnPlaybackStopped when the explicit stop is handled.
    private volatile bool _explicitStopRequested = false;


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
        // Also, check if audioFileReader and CurrentSong are still valid.
        if (_waveOutDevice?.PlaybackState == PlaybackState.Playing && audioFileReader != null && CurrentSong != null)
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
                    // PlaybackStopped event should handle final state if it fires due to this error.
                    return;
                }

                // Update the ViewModel property, which will notify UI bindings
                this.CurrentPosition = currentAudioTime; // Update CurrentPosition via its private setter

                // Note: Loop region handling is within the PlaybackService itself,
                // as it directly affects seeking logic during playback.
                if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
                {
                    var loop = CurrentSong.SavedLoop;
                    // Ensure loop end is after loop start and valid within total time
                    if (loop.End > loop.Start && loop.End <= audioFileReader.TotalTime)
                    {
                        // Check if current position is at or past the loop end
                        // Using a small tolerance (e.g., 50ms) to trigger seek slightly before the exact end,
                        // but ensure it's not extremely close to the *total* song duration.
                        TimeSpan seekThreshold = loop.End - TimeSpan.FromMilliseconds(50);
                        if (currentAudioTime >= seekThreshold && currentAudioTime < audioFileReader.TotalTime - TimeSpan.FromMilliseconds(200))
                        {
                            Debug.WriteLine($"[PlaybackService] Loop active & end reached ({currentAudioTime:mm\\:ss\\.ff} >= {seekThreshold:mm\\:ss\\.ff}) within file ({audioFileReader.TotalTime:mm\\:ss\\.ff}). Seeking to loop start: {loop.Start:mm\\:ss\\.ff}");
                            Seek(loop.Start); // Perform the seek. Seek() handles its own logging and position update.
                            // After seeking, the timer will continue, reading the new position.
                        }
                        // If currentAudioTime is >= loop.End but also very close to audioFileReader.TotalTime,
                        // we let the natural end-of-file event trigger (PlaybackStopped).
                    }
                    else if (CurrentSong.IsLoopActive)
                    {
                        Debug.WriteLine($"[PlaybackService] Loop active but invalid region ({loop.Start:mm\\:ss\\.ff} - {loop.End:mm\\:ss\\.ff}). Loop will not function.");
                    }
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
        // The _explicitStopRequested flag is ONLY set by public Stop().
        // Do NOT set _explicitStopRequested = false here.

        // If a song is currently playing/paused, initiate stop of the old one first.
        // This triggers OnPlaybackStopped for the old device, which cleans up.
        // If the device is already stopped or null, InitiateStop() does nothing.
        // CleanUpNAudioResources() below ensures a clean state regardless.
        if (_waveOutDevice != null && (_waveOutDevice.PlaybackState == PlaybackState.Playing || _waveOutDevice.PlaybackState == PlaybackState.Paused))
        {
            Debug.WriteLine("[PlaybackService] Play called while device active. Initiating stop of old playback.");
            InitiateStop(); // This triggers OnPlaybackStopped for the old device asynchronously
        }
        else if (_waveOutDevice != null && _waveOutDevice.PlaybackState == PlaybackState.Stopped)
        {
            // Device exists but is stopped. Its event handler should have run. Ensure clean state before Init.
            Debug.WriteLine("[PlaybackService] Play called while device stopped. Proceeding with Init.");
        }
        else // _waveOutDevice is null
        {
            Debug.WriteLine("[PlaybackService] Play called with no active device. Proceeding with Init.");
        }

        // Ensure cleanup happens before initializing the new pipeline, regardless of previous state.
        // This is called on the UI thread as Play() is called on the UI thread by MainVM.
        CleanUpNAudioResources(); // Dispose the old resources before creating new ones.


        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[PlaybackService] Play called with null/invalid/missing file song. Cleaning up and stopping.");
            // Use the public Stop() for a full reset including nulling CurrentSong
            Stop(); // This will set _explicitStopRequested=true and call InitiateStop/OnPlaybackStopped path
            return;
        }

        // Set the new current song. This will notify UI and other VMs.
        // Set BEFORE initializing the pipeline, so Init can use CurrentSong properties if needed.
        CurrentSong = song;
        // Other state properties (IsPlaying, Status, Position, Duration) will be set by InitializeNAudioPipeline or Play().

        // Attempt to initialize the NAudio pipeline for the new song.
        bool pipelineInitialized = InitializeNAudioPipeline(song.FilePath);

        if (pipelineInitialized && _waveOutDevice != null && audioFileReader != null)
        {
            // If a loop is active for the new song, seek to the start of the loop before playing
            // Ensure loop start is valid and within total time
            if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null && CurrentSong.SavedLoop.Start >= TimeSpan.Zero && CurrentSong.SavedLoop.Start < audioFileReader.TotalTime)
            {
                Debug.WriteLine($"[PlaybackService] New song has active loop ({CurrentSong.SavedLoop.Start:mm\\:ss\\.ff} - {CurrentSong.SavedLoop.End:mm\\:ss\\.ff}). Seeking to loop start: {CurrentSong.SavedLoop.Start:mm\\:ss\\.ff} before playing.");
                // Seek handles clamping and potential tolerance.
                Seek(CurrentSong.SavedLoop.Start);
                // Note: This seek is best-effort. Playback might start slightly before or after the exact time.
            }
            else if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
            {
                Debug.WriteLine($"[PlaybackService] New song has active loop ({CurrentSong.SavedLoop.Start:mm\\:ss\\.ff} - {CurrentSong.SavedLoop.End:mm\\:ss\\.ff}), but loop start is invalid ({CurrentSong.SavedLoop.Start >= audioFileReader.TotalTime}). Starting from beginning.");
                // Position is already TimeSpan.Zero from InitializeNAudioPipeline. No seek needed.
            }
            else
            {
                Debug.WriteLine("[PlaybackService] New song has no active loop. Starting from beginning.");
                // Position is already TimeSpan.Zero from InitializeNAudioPipeline. No seek needed.
            }


            // Start the playback device
            _waveOutDevice.Play();
            // Update state properties to reflect playing status *after* successful Play() call
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            // Start the timer to update UI position
            StartUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Playback started for: {CurrentSong.Title}. State: {CurrentPlaybackStatus}");

            // Reset the manual stop flag if it was true when Play() was called,
            // as a new track is now playing, regardless of a pending manual stop signal.
            // This handles the edge case where user clicked Stop then immediately Play.
            if (_explicitStopRequested)
            {
                _explicitStopRequested = false;
                Debug.WriteLine("[PlaybackService] _explicitStopRequested reset to false by Play() because new playback started.");
            }


        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Pipeline init failed for {Path.GetFileName(song.FilePath)}. Cleaning up and stopping.");
            // If initialization failed, perform a full stop to reset all related state cleanly.
            Stop(); // Use the public Stop() for a full reset (sets _explicitStopRequested=true, initiates stop, OnPlaybackStopped handles nulling CurrentSong)
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
        // CleanUpNAudioResources() should dispose previous instances and nullify fields.
        // This is called by Play() before starting initialization.
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
            // Store the instance reference BEFORE attaching the handler and BEFORE Init
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
            // No need to call CleanUpNAudioResources here, as Play() handles the full stop.
            // CleanUpNAudioResources(); // Should not be called here, Play() will call Stop()
            // Reset duration and position if init failed (already done by Stop() called in Play())
            // CurrentSongDuration = TimeSpan.Zero;
            // this.CurrentPosition = TimeSpan.Zero;
            // The caller (Play method) handles setting the global state to Stopped if Init fails.
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
        Debug.WriteLine("[PlaybackService] UI Update Timer Started.");
    }

    /// <summary>
    /// Stops the timer used for periodically updating the UI position.
    /// </summary>
    private void StopUiUpdateTimer()
    {
        // Stop the timer by setting interval to Infinite
        uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        Debug.WriteLine("[PlaybackService] UI Update Timer Stopped.");
    }

    /// <summary>
    /// Cleans up and disposes of NAudio playback resources.
    /// This should be called whenever playback stops or before initializing a new pipeline.
    /// Assumed to be called on the UI thread or a thread safe context.
    /// This method disposes the *current* service resources (_waveOutDevice, audioFileReader etc.)
    /// </summary>
    private void CleanUpNAudioResources()
    {
        Debug.WriteLine("[PlaybackService] CleanUpNAudioResources called.");
        // Ensure timer is stopped before disposing resources it might access.
        StopUiUpdateTimer();

        // Ensure we only detach the event from the *currently active* instance that we tracked, if it's not null.
        // Detach *before* disposing.
        if (_waveOutDevice != null && _waveOutDeviceInstanceForStopEventCheck == _waveOutDevice)
        {
            _waveOutDevice.PlaybackStopped -= OnPlaybackStopped; // Detach handler
            Debug.WriteLine("[PlaybackService] Detached PlaybackStopped handler.");
        }
        _waveOutDeviceInstanceForStopEventCheck = null; // Clear the tracked instance reference regardless

        // Dispose the wave output device if it exists and is not already disposed
        if (_waveOutDevice != null)
        {
            Debug.WriteLine($"[PlaybackService] Disposing WaveOutDevice (State: {_waveOutDevice.PlaybackState}).");
            // Stopping the device here is generally not needed if device.Stop() was called before cleanup,
            // but Dispose() implicitly stops if playing/paused.
            try { _waveOutDevice.Dispose(); } // Dispose the device
            catch (Exception ex) { Debug.WriteLine($"[PlaybackService] Error during _waveOutDevice.Dispose() in cleanup: {ex.Message}"); }
            _waveOutDevice = null; // Nullify the service reference
        }

        // Dispose the audio file reader if it exists
        if (audioFileReader != null)
        {
            Debug.WriteLine("[PlaybackService] Disposing AudioFileReader.");
            try { audioFileReader.Dispose(); }
            catch (Exception ex) { Debug.WriteLine($"[PlaybackService] Error during audioFileReader.Dispose() in cleanup: {ex.Message}"); }
            audioFileReader = null; // Nullify the service reference
        }

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
    /// <param name="e">The event arguments, including any exception if an occurred.</param>
    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // This code runs on the UI thread because it's wrapped in Dispatcher.UIThread.InvokeAsync.

        // Check if the sender is the expected device instance to avoid handling events from old/disposed devices.
        // Note: _waveOutDeviceInstanceForStopEventCheck might be null here if CleanUpNAudioResources
        // was called by Play() on an already stopped device before this handler ran for the old device.
        // However, the sender is the actual instance that stopped. Checking sender reference against
        // the *service's current* _waveOutDevice is unreliable if Play() just started a new one.
        // The instance check should be against the *captured* instance reference from before cleanup.
        // This seems complex. Let's simplify and assume the handler only fires for the instance
        // that was active when Stop() or end-of-file occurred. CleanUpNAudioResources nullifies
        // the service field after disposing, so relying on the *captured* flags and state is better.


        // Capture flags and state *before* CleanUpNAudioResources potentially nullifies fields.
        // The _explicitStopRequested flag should reflect the intention *when the stop happened*.
        // Since Play() no longer sets _explicitStopRequested=false, this flag accurately reflects
        // whether public Stop() was called (true) or not (false).
        bool wasExplicitStop = _explicitStopRequested; // Capture state of flag

        // Attempt to get state information from the service fields *before* CleanUpResources disposes them.
        Song? songThatStopped = CurrentSong; // Capture song reference
        TimeSpan lastKnownPosition = TimeSpan.Zero;
        TimeSpan? songDuration = CurrentSongDuration.TotalSeconds > 0 ? (TimeSpan?)CurrentSongDuration : null;

        try
        {
            // Attempt to get the position from the reader before cleanup.
            // This might fail if resources were already disposed by a concurrent Play() call.
            if (audioFileReader != null)
            {
                lastKnownPosition = audioFileReader.CurrentTime;
            }
        }
        catch (Exception posEx)
        {
            Debug.WriteLine($"[PlaybackService] Error getting last known position before cleanup in OnPlaybackStopped: {posEx.Message}.");
            // lastKnownPosition remains TimeSpan.Zero on error.
        }

        // Safely format captured properties for logging
        TimeSpan? loopStart = songThatStopped?.SavedLoop?.Start;
        TimeSpan? loopEnd = songThatStopped?.SavedLoop?.End;
        string loopStartFormatted = loopStart.HasValue ? $"{loopStart.Value:mm\\:ss\\.ff}" : "N/A";
        string loopEndFormatted = loopEnd.HasValue ? $"{loopEnd.Value:mm\\:ss\\.ff}" : "N/A";
        string songDurationFormatted = songDuration.HasValue ? $"{songDuration.Value:mm\\:ss\\.ff}" : "N/A";

        Debug.WriteLine($"[PlaybackService] === OnPlaybackStopped START === (UI Thread)");
        Debug.WriteLine($"[PlaybackService] Event Sender Type: {sender?.GetType().Name ?? "null"}");
        Debug.WriteLine($"[PlaybackService] Current Service Device: {_waveOutDevice?.GetType().Name ?? "null"} (State: {_waveOutDevice?.PlaybackState})");
        Debug.WriteLine($"[PlaybackService] Exception: {e.Exception?.Message ?? "None"}");
        Debug.WriteLine($"[PlaybackService] _explicitStopRequested (captured state): {wasExplicitStop}");
        Debug.WriteLine($"[PlaybackService] Song that stopped (captured): {songThatStopped?.Title ?? "null"}"); // Log captured song
        Debug.WriteLine($"[PlaybackService] Current Pos Before Cleanup Attempt: {lastKnownPosition:mm\\:ss\\.ff}");
        Debug.WriteLine($"[PlaybackService] Song Duration (captured): {songDurationFormatted}");
        Debug.WriteLine($"[PlaybackService] Song LoopActive (captured): {songThatStopped?.IsLoopActive ?? false}, Loop: {loopStartFormatted} - {loopEndFormatted}");
        Debug.WriteLine($"[PlaybackService] === OnPlaybackStopped END initial checks ===");


        // Clean up resources associated with the service.
        // This disposes the *current* service resources (_waveOutDevice, audioFileReader etc)
        // It also nullifies the service fields and detaches the event handler from the instance it disposes.
        // This must happen on the UI thread as the handler is dispatched there.
        CleanUpNAudioResources(); // Disposes the sender device/reader and nullifies service fields

        // Stop the UI timer.
        StopUiUpdateTimer();

        // Update service state on the UI thread based on the reason for stopping.
        if (e.Exception != null)
        {
            Debug.WriteLine($"[PlaybackService] Playback stopped due to error: {e.Exception.Message}. Finalizing state to Stopped.");
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            this.CurrentPosition = TimeSpan.Zero;
            CurrentSongDuration = TimeSpan.Zero; // Error means no valid duration
            CurrentSong = null; // On error, clear the song reference
            // Reset manual stop flag on error, as the explicit stop sequence is broken.
            _explicitStopRequested = false;
            Debug.WriteLine("[PlaybackService] _explicitStopRequested reset to false by error handler.");
            Debug.WriteLine("[PlaybackService] State set to Stopped by error handler.");
        }
        else // e.Exception == null (Clean stop)
        {
            // Determine if it was a natural end of file.
            // This is true if it's a clean stop, was NOT an explicit manual stop (_explicitStopRequested was false),
            // AND the position was near the end of the song that stopped.
            bool isNearEndOfFile = songDuration.HasValue && lastKnownPosition >= songDuration.Value - TimeSpan.FromMilliseconds(200);

            Debug.WriteLine($"[PlaybackService] Clean Stop. Was Explicit Stop (captured): {wasExplicitStop}. Is Near End of File: {isNearEndOfFile}.");

            if (wasExplicitStop) // Case 1: Manual Stop via public Stop()
            {
                Debug.WriteLine("[PlaybackService] Playback stopped by explicit user command (event). Finalizing state to Stopped.");
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                this.CurrentPosition = TimeSpan.Zero;
                CurrentSongDuration = TimeSpan.Zero; // Manual stop clears duration/song
                CurrentSong = null; // This is the full stop state.
                // Reset the flag now that the explicit stop is fully handled.
                _explicitStopRequested = false;
                Debug.WriteLine("[PlaybackService] _explicitStopRequested reset to false after handling explicit stop.");
                Debug.WriteLine("[PlaybackService] State set to Stopped by explicit stop handler.");
            }
            else if (isNearEndOfFile) // Case 2: Natural End of File (and not an explicit stop)
            {
                Debug.WriteLine("[PlaybackService] Playback stopped naturally (event). Raising PlaybackEndedNaturally event.");
                // Natural end signals, and sets state to Stopped.
                // The MainVM handler will then call Play() or Stop(), which sets the next state (Playing or staying Stopped).
                // Set position to 0 for the NEXT song/loop start.
                this.CurrentPosition = TimeSpan.Zero;
                // State should reflect Stopped after ANY stop event.
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                // Duration and Song reference remain for the event handler to use.

                PlaybackEndedNaturally?.Invoke(this, EventArgs.Empty);
                Debug.WriteLine("[PlaybackService] State set to Stopped by natural end handler, event raised.");
            }
            else
            {
                // Case 3: Clean stop that was not explicit and not near end. Implies interruption by Play().
                Debug.WriteLine("Playback stopped by interruption (likely Play called).");
                // Resources cleaned up. Service state (IsPlaying, CurrentPlaybackStatus, CurrentSong, Pos, Duration)
                // is handled by the NEW Play() call that caused this interruption.
                // Do NOT change service state here. It should reflect the *new* state.
                // Do NOT reset _explicitStopRequested (it should already be false).
                // Just log and exit. The new playback state is already being set by Play().
                Debug.WriteLine("[PlaybackService] Interruption stop handler finished. State managed by calling Play() method.");
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
            Play(CurrentSong);
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Resume requested but conditions not met. Device State: {_waveOutDevice?.PlaybackState ?? PlaybackState.Stopped}, AFR: {audioFileReader != null}, Current Playback Status: {CurrentPlaybackStatus}. Doing nothing.");
        }
    }

    /// <summary>
    /// Initiates the stopping process. Signals the device to stop.
    /// Cleanup and state updates happen in the PlaybackStopped event handler.
    /// </summary>
    private void InitiateStop() // Renamed from StopPlaybackInternal, removed bool parameter
    {
        Debug.WriteLine($"[PlaybackService] InitiateStop called. Current device state: {_waveOutDevice?.PlaybackState}. _explicitStopRequested = {_explicitStopRequested}");

        if (_waveOutDevice != null)
        {
            if (_waveOutDevice.PlaybackState != PlaybackState.Stopped)
            {
                Debug.WriteLine("[PlaybackService] Calling device.Stop() from InitiateStop.");
                try
                {
                    // This will trigger OnPlaybackStopped
                    _waveOutDevice.Stop();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PlaybackService] Error calling _waveOutDevice.Stop() in InitiateStop: {ex.Message}. This might prevent OnPlaybackStopped from firing for this device. State might become inconsistent.");
                    // If Stop() fails, the event might not fire. The state might not be updated correctly.
                    // The Play() method's subsequent CleanUpNAudioResources might help recover,
                    // but the state could briefly show Playing when it shouldn't.
                }
            }
            else
            {
                // Device is already stopped. OnPlaybackStopped won't fire for this instance via Stop().
                Debug.WriteLine("[PlaybackService] InitiateStop: Device already stopped. No Stop() call needed. Cleanup will happen during new Init or via pending OnPlaybackStopped.");
            }
        }
        else
        {
            // No active device. Playback is already fully stopped.
            Debug.WriteLine("[PlaybackService] InitiateStop: No active device found. Already stopped.");
        }

        // The state reset (CurrentSong = null etc.) is handled by OnPlaybackStopped for explicit stops.
        // For interruptions (Play called), the new Play() call handles setting the state.
    }

    /// <summary>
    /// Public method to stop playback completely and reset the current song state.
    /// </summary>
    public void Stop()
    {
        Debug.WriteLine("[PlaybackService] Public Stop() called.");
        _explicitStopRequested = true; // Signal user stop intention
        InitiateStop(); // Trigger the stop process
        // State reset (CurrentSong = null etc.) is handled by OnPlaybackStopped if it sees _explicitStopRequested == true.
        // The flag will be reset to false in OnPlaybackStopped after handling.
    }

    /// <summary>
    /// Seeks to a specific position in the current song.
    /// Respects active loop regions by snapping the target position if needed.
    /// </summary>
    /// <param name="requestedPosition">The desired time position.</param>
    public void Seek(TimeSpan requestedPosition)
    {
        // Check if there's a song loaded, audio file reader is initialized, and device is not stopped.
        // We need audioFileReader to get TotalTime and set CurrentTime.
        // We need _waveOutDevice to be non-null and not Stopped to ensure the reader is in a state where seeking is valid.
        if (audioFileReader == null || CurrentSong == null || _waveOutDevice?.PlaybackState == PlaybackState.Stopped)
        {
            Debug.WriteLine($"[PlaybackService] Seek ignored: No active audio file reader, current song, or device is stopped. AFR: {audioFileReader != null}, Song: {CurrentSong != null}, Device State: {_waveOutDevice?.PlaybackState}");
            return;
        }

        TimeSpan targetPosition = requestedPosition;

        // Apply loop region constraints if an active loop is defined for the current song.
        // If seeking *into* an active loop from *outside* its start or after its end, snap to start.
        // If seeking *within* an active loop, allow it.
        if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
        {
            var loop = CurrentSong.SavedLoop;
            // Ensure loop end is after loop start and valid within total time
            if (loop.End > loop.Start && loop.End <= audioFileReader.TotalTime)
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
            else if (CurrentSong.IsLoopActive)
            {
                Debug.WriteLine($"[PlaybackService] Seek: Loop active but invalid region ({loop.Start:mm\\:ss\\.ff} - {loop.End:mm\\:ss\\.ff}). Not applying loop seek constraints.");
            }
        }

        // Clamp targetPosition to a valid range within the audio file's total duration.
        // Seeking exactly to TotalTime can sometimes cause issues with readers.
        // Subtract a small margin (e.g., 100ms) from the total time to get the maximum seekable position.
        // Ensure the margin doesn't make the max seekable position negative for very short files.
        var totalMs = audioFileReader.TotalTime.TotalMilliseconds;
        var seekMarginMs = totalMs > 200 ? 100 : (totalMs > 0 ? Math.Min(totalMs / 2, 50) : 0); // Subtract 100ms if over 200ms, or a smaller amount for shorter files, min 0
        var maxSeekablePosition = TimeSpan.FromMilliseconds(totalMs - seekMarginMs);
        // Ensure max seekable position is not less than zero.
        if (maxSeekablePosition < TimeSpan.Zero) maxSeekablePosition = TimeSpan.Zero;

        // Apply clamping to the target position
        targetPosition = TimeSpan.FromSeconds(Math.Clamp(targetPosition.TotalSeconds, 0, maxSeekablePosition.TotalSeconds));

        // Add a small tolerance check to avoid seeking if the target is very close to the current position.
        // This can reduce unnecessary operations from minor slider fluctuations and potentially prevent tight seeking loops.
        // The tolerance should be larger for manual seeks (like slider) than for internal loops.
        // Let's use a slightly larger tolerance for general seeking.
        double positionToleranceSeconds = 0.3; // 300 milliseconds tolerance

        try
        {
            // Safely get current time for tolerance check. Ensure audioFileReader is not null after clamping.
            if (audioFileReader != null)
            {
                TimeSpan currentAudioTimeForToleranceCheck = audioFileReader.CurrentTime;

                if (Math.Abs(currentAudioTimeForToleranceCheck.TotalSeconds - targetPosition.TotalSeconds) < positionToleranceSeconds)
                {
                    Debug.WriteLine($"[PlaybackService] Seek target {targetPosition:mm\\:ss\\.ff} is very close to current position {currentAudioTimeForToleranceCheck:mm\\:ss\\.ff} (within {positionToleranceSeconds}s), ignoring seek.");
                    // We don't explicitly update this.CurrentPosition here; it will be updated by the timer callback if playing.
                    return; // Skip the seek if within tolerance
                }
            }
            else
            {
                Debug.WriteLine($"[PlaybackService] Seek: audioFileReader is null during tolerance check. Proceeding with seek attempt.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Error checking current position for seek tolerance: {ex.Message}. Proceeding with seek.");
            // Ignore error in tolerance check, continue with seek
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
                Debug.WriteLine($"[PlaybackService] Seek failed: audioFileReader is null after checks.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL Error during Seek to {targetPosition:mm\\:ss\\.ff}: {ex.Message}");
            // Update VM position to reflect the last known good position, or zero on error
            // If audioFileReader is still valid, try to read current position after failed seek attempt.
            if (audioFileReader != null)
            {
                try { this.CurrentPosition = audioFileReader.CurrentTime; }
                catch (Exception readEx) { Debug.WriteLine($"[PlaybackService] Error reading position after failed seek: {readEx.Message}"); }
            }
            else
            {
                // If audioFileReader is null, set position to 0
                this.CurrentPosition = TimeSpan.Zero;
            }
            // Set state to stopped? No, let the PlaybackStopped handler or Play() handle state transitions.
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
        // This also detaches the PlaybackStopped event handler *from the instance being disposed*.
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
    // Finalizers are generally discouraged unless strictly necessary due to performance overhead and complexity.
    // Our App's OnFrameworkInitializationCompleted manages the lifetime, so Dispose should be called explicitly.
    // Let's keep the finalizer as a safeguard, but rely on explicit Dispose.
    ~PlaybackService()
    {
        Debug.WriteLine("[PlaybackService] Finalizer called for PlaybackService.");
        // Call the public Dispose method.
        Dispose(); // Call the public Dispose method to perform cleanup
        Debug.WriteLine("[PlaybackService] Finalizer completed for PlaybackService.");
    }
}