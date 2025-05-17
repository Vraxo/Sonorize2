using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.ViewModels;
using System;
using System.Diagnostics;
using System.Threading;
using ManagedBass;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Un4seen.Bass.AddOn.Fx;

namespace Sonorize.Services;

public enum PlaybackStateStatus { Stopped, Playing, Paused }

public class PlaybackService : ViewModelBase, IDisposable
{
    private static bool _resolverHooked = false;
    private static string? _applicationBaseDirectory = null;
    private const int UiUpdateIntervalMilliseconds = 100;

    static PlaybackService()
    {
        try
        {
            _applicationBaseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Debug.WriteLine($"[PlaybackService StaticCtor] Application Base Directory: {_applicationBaseDirectory ?? "Not determined"}");

            if (!_resolverHooked && _applicationBaseDirectory != null)
            {
                // Resolver for ManagedBass.dll (for bass.dll)
                Assembly bassAssembly = typeof(Bass).Assembly;
                NativeLibrary.SetDllImportResolver(bassAssembly, ResolveBassLibraryFromAppDirectory);
                Debug.WriteLine($"[PlaybackService StaticCtor] DllImportResolver for ManagedBass assembly ('{bassAssembly.FullName}') has been set.");

                // Resolver for ManagedBass.Fx.dll (for bass_fx.dll)
                try
                {
                    Assembly bassFxAssembly = typeof(BassFx).Assembly;
                    if (bassFxAssembly != bassAssembly) // Only set if it's a different assembly
                    {
                        NativeLibrary.SetDllImportResolver(bassFxAssembly, ResolveBassLibraryFromAppDirectory);
                        Debug.WriteLine($"[PlaybackService StaticCtor] DllImportResolver for ManagedBass.Fx assembly ('{bassFxAssembly.FullName}') has been set.");
                    }
                    else
                    {
                        Debug.WriteLine("[PlaybackService StaticCtor] ManagedBass.Fx types appear to be in the same assembly as core ManagedBass. One resolver is sufficient.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PlaybackService StaticCtor] Could not set DllImportResolver for ManagedBass.Fx: {ex.Message}. This might be expected if ManagedBass.Fx is not used/referenced, or if P/Invokes are in the core assembly.");
                }
                _resolverHooked = true;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _applicationBaseDirectory != null)
            {
                Debug.WriteLine($"[PlaybackService StaticCtor] Windows OS detected. Attempting to call SetDllDirectory with: {_applicationBaseDirectory}");
                if (NativeMethods.SetDllDirectory(_applicationBaseDirectory))
                {
                    Debug.WriteLine("[PlaybackService StaticCtor] SetDllDirectory call succeeded.");
                }
                else
                {
                    Debug.WriteLine($"[PlaybackService StaticCtor] SetDllDirectory call failed. Win32Error: {Marshal.GetLastWin32Error()}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService StaticCtor] CRITICAL EXCEPTION during static initialization: {ex}");
        }
    }

    private static IntPtr ResolveBassLibraryFromAppDirectory(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        Debug.WriteLine($"[ResolveBassLibrary] Attempting to resolve: '{libraryName}' for assembly '{assembly.FullName}' with searchPath '{searchPath}'. AppBaseDir: '{_applicationBaseDirectory}'");
        IntPtr libHandle = IntPtr.Zero;

        if (string.IsNullOrEmpty(_applicationBaseDirectory))
        {
            Debug.WriteLine($"[ResolveBassLibrary] ApplicationBaseDirectory is not set. Cannot perform custom resolution for '{libraryName}'.");
            return IntPtr.Zero;
        }

        string platformSpecificFileName;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) platformSpecificFileName = $"{libraryName}.dll";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) platformSpecificFileName = $"lib{libraryName}.so";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) platformSpecificFileName = $"lib{libraryName}.dylib";
        else
        {
            platformSpecificFileName = libraryName;
            Debug.WriteLine($"[ResolveBassLibrary] Unknown OS platform. Using library name as is: {libraryName}");
        }
        Debug.WriteLine($"[ResolveBassLibrary] Platform-specific name for '{libraryName}' is '{platformSpecificFileName}'.");

        string potentialPath = Path.Combine(_applicationBaseDirectory, platformSpecificFileName);
        Debug.WriteLine($"[ResolveBassLibrary] Checking path: {potentialPath}");
        if (File.Exists(potentialPath))
        {
            if (NativeLibrary.TryLoad(potentialPath, out libHandle))
            {
                Debug.WriteLine($"[ResolveBassLibrary] Successfully loaded '{libraryName}' from '{potentialPath}'. Handle: {libHandle}");
                return libHandle;
            }
            Debug.WriteLine($"[ResolveBassLibrary] NativeLibrary.TryLoad failed for '{potentialPath}' even though file exists. Handle: {libHandle}");
        }
        else Debug.WriteLine($"[ResolveBassLibrary] File not found at '{potentialPath}'.");

        string? rid = null;
        string os = "";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) os = "win";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) os = "linux";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) os = "osx";

        if (!string.IsNullOrEmpty(os))
        {
            string arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm64 => "arm64",
                Architecture.Arm => "arm",
                _ => ""
            };
            if (!string.IsNullOrEmpty(arch)) rid = $"{os}-{arch}";
        }

        if (!string.IsNullOrEmpty(rid))
        {
            potentialPath = Path.Combine(_applicationBaseDirectory, "runtimes", rid, "native", platformSpecificFileName);
            Debug.WriteLine($"[ResolveBassLibrary] Checking RID-specific path: {potentialPath}");
            if (File.Exists(potentialPath))
            {
                if (NativeLibrary.TryLoad(potentialPath, out libHandle))
                {
                    Debug.WriteLine($"[ResolveBassLibrary] Successfully loaded '{libraryName}' from '{potentialPath}'. Handle: {libHandle}");
                    return libHandle;
                }
                Debug.WriteLine($"[ResolveBassLibrary] NativeLibrary.TryLoad failed for '{potentialPath}' even though file exists. Handle: {libHandle}");
            }
            else Debug.WriteLine($"[ResolveBassLibrary] File not found at '{potentialPath}'.");
        }
        else Debug.WriteLine("[ResolveBassLibrary] Could not determine RID to check runtime-specific path.");

        Debug.WriteLine($"[ResolveBassLibrary] Failed to resolve '{libraryName}' using custom strategies. Falling back to default loader.");
        return IntPtr.Zero;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetDllDirectory(string? lpPathName);
    }

    private Song? _currentSong;
    public Song? CurrentSong { get => _currentSong; private set { SetProperty(ref _currentSong, value); OnPropertyChanged(nameof(HasCurrentSong)); } }
    public bool HasCurrentSong => CurrentSong != null;

    private bool _isPlaying;
    public bool IsPlaying { get => _isPlaying; private set => SetProperty(ref _isPlaying, value); }

    private PlaybackStateStatus _currentPlaybackStatus = PlaybackStateStatus.Stopped;
    public PlaybackStateStatus CurrentPlaybackStatus { get => _currentPlaybackStatus; private set => SetProperty(ref _currentPlaybackStatus, value); }

    private TimeSpan _currentPosition;
    public TimeSpan CurrentPosition { get => _currentPosition; set { if (SetProperty(ref _currentPosition, value)) OnPropertyChanged(nameof(CurrentPositionSeconds)); } }
    public double CurrentPositionSeconds { get => CurrentPosition.TotalSeconds; set { if (_streamHandle != 0 && Math.Abs(CurrentPosition.TotalSeconds - value) > 0.1) Seek(TimeSpan.FromSeconds(value)); } }

    private TimeSpan _currentSongDuration;
    public TimeSpan CurrentSongDuration { get => _currentSongDuration; private set { if (SetProperty(ref _currentSongDuration, value)) OnPropertyChanged(nameof(CurrentSongDurationSeconds)); } }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1;

    private Timer? uiUpdateTimer;
    private SyncProcedure? _endSyncProc;

    private int _streamHandle;
    private int _tempoStreamHandle;

    private float _playbackRate = 1.0f;
    public float PlaybackRate { get => _playbackRate; set { if (SetProperty(ref _playbackRate, value)) { if (_tempoStreamHandle != 0) { float tempoPercentage = (_playbackRate - 1.0f) * 100.0f; Bass.ChannelSetAttribute(_tempoStreamHandle, ChannelAttribute.Tempo, tempoPercentage); } } } }

    private float _pitchSemitones = 0f;
    public float PitchSemitones { get => _pitchSemitones; set { if (SetProperty(ref _pitchSemitones, value)) { if (_tempoStreamHandle != 0) { Bass.ChannelSetAttribute(_tempoStreamHandle, ChannelAttribute.Pitch, _pitchSemitones); } } } }

    private static bool _bassSuccessfullyInitialized = false;

    public PlaybackService()
    {
        if (!_bassSuccessfullyInitialized)
        {
            Debug.WriteLine("[PlaybackService Ctor] Attempting Bass.Init()...");
            if (!Bass.Init(-1, 44100, DeviceInitFlags.Default, IntPtr.Zero))
            {
                var error = Bass.LastError;
                Debug.WriteLine($"[PlaybackService Ctor] Bass.Init failed! Error: {error}. Ensure native BASS libraries (e.g., bass.dll) are correctly deployed.");
            }
            else
            {
                _bassSuccessfullyInitialized = true;
                Debug.WriteLine($"[PlaybackService Ctor] Bass.Init successful. BASS Version: {Bass.Version}");

                // Optionally check BASS FX version if Bass.Init succeeded and ManagedBass.Fx is used.
                // var fxVersion = ManagedBass.Fx.BassFx.Version; // Or just BassFx.Version with using ManagedBass.Fx;
                // Debug.WriteLine($"[PlaybackService Ctor] BassFx Version: {fxVersion}. (If 0, bass_fx library might not be loaded/found).");
            }
        }
        _endSyncProc = new SyncProcedure(PlaybackEndedCallback);
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void UpdateUiCallback(object? state)
    {
        if (IsPlaying && _tempoStreamHandle != 0 && Bass.ChannelIsActive(_tempoStreamHandle) == PlaybackState.Playing)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_streamHandle == 0) return; // Stream might have been disposed concurrently

                long currentBytePos = Bass.ChannelGetPosition(_streamHandle, PositionFlags.Bytes);
                CurrentPosition = TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_streamHandle, currentBytePos));

                if (CurrentSong?.ActiveLoop != null)
                {
                    var activeLoop = CurrentSong.ActiveLoop;
                    if (CurrentPosition >= activeLoop.End && activeLoop.End > activeLoop.Start)
                    {
                        Seek(activeLoop.Start);
                    }
                }
            });
        }
    }

    private void InitializeBassPipeline(string filePath)
    {
        CleanUpPlaybackResources();

        _streamHandle = Bass.CreateStream(filePath, 0, 0, BassFlags.Decode | BassFlags.Prescan | BassFlags.Float);
        if (_streamHandle == 0)
        {
            Debug.WriteLine($"[PlaybackService] Bass.CreateStream failed for '{filePath}'. Error: {Bass.LastError}");
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            return;
        }

        _tempoStreamHandle = BassFx.BASS_FX_TempoCreate(_streamHandle, (Un4seen.Bass.BASSFlag)(BassFlags.FxFreeSource | BassFlags.Float));
        if (_tempoStreamHandle == 0)
        {
            Debug.WriteLine($"[PlaybackService] BassFx.TempoCreate failed. Error: {Bass.LastError}. This often means 'bass_fx.dll' (or equivalent) was not loaded.");
            Bass.StreamFree(_streamHandle);
            _streamHandle = 0;
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            return;
        }

        // Apply current rate and pitch to the new tempo stream
        Bass.ChannelSetAttribute(_tempoStreamHandle, ChannelAttribute.Tempo, (PlaybackRate - 1.0f) * 100.0f);
        Bass.ChannelSetAttribute(_tempoStreamHandle, ChannelAttribute.Pitch, PitchSemitones);

        long lengthBytes = Bass.ChannelGetLength(_streamHandle, PositionFlags.Bytes);
        CurrentSongDuration = TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_streamHandle, lengthBytes));
        CurrentPosition = TimeSpan.Zero;

        if (_endSyncProc != null)
        {
            Bass.ChannelSetSync(_tempoStreamHandle, SyncFlags.End | SyncFlags.Mixtime, 0, _endSyncProc, IntPtr.Zero);
        }
    }

    public void Play(Song song)
    {
        if (song == null) return;
        CurrentSong = song;
        try
        {
            InitializeBassPipeline(song.FilePath);
            if (_tempoStreamHandle != 0)
            {
                if (Bass.ChannelPlay(_tempoStreamHandle, false))
                {
                    IsPlaying = true;
                    CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                    StartUiUpdateTimer();
                }
                else
                {
                    Debug.WriteLine($"[PlaybackService] Bass.ChannelPlay failed. Error: {Bass.LastError}");
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    CleanUpPlaybackResources();
                }
            }
            else
            {
                IsPlaying = false;
                CurrentSongDuration = TimeSpan.Zero;
                CurrentPosition = TimeSpan.Zero;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Exception during Play for '{song.FilePath}': {ex}");
            IsPlaying = false;
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            CleanUpPlaybackResources();
        }
    }

    private void StartUiUpdateTimer() => uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(UiUpdateIntervalMilliseconds));

    private void StopUiUpdateTimer() => uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);

    private void CleanUpPlaybackResources()
    {
        StopUiUpdateTimer();
        if (_tempoStreamHandle != 0)
        {
            Bass.StreamFree(_tempoStreamHandle); // Frees _tempoStreamHandle and _streamHandle (due to BassFlags.FxFreeSource)
            _tempoStreamHandle = 0;
            _streamHandle = 0;
        }
        else if (_streamHandle != 0) // If only _streamHandle was created (e.g., TempoCreate failed)
        {
            Bass.StreamFree(_streamHandle);
            _streamHandle = 0;
        }
    }

    private void PlaybackEndedCallback(int Handle, int Channel, int Data, IntPtr User)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Channel == _tempoStreamHandle)
            {
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                StopUiUpdateTimer();
                CurrentPosition = TimeSpan.Zero;
                Debug.WriteLine($"[PlaybackService] Playback ended via callback for handle {Channel}");
                // Optionally, could call CleanUpPlaybackResources() here if appropriate for app logic,
                // but typically resources are kept for potential replay/seek unless explicitly stopped.
            }
        });
    }

    public void Pause()
    {
        if (IsPlaying && _tempoStreamHandle != 0 && Bass.ChannelIsActive(_tempoStreamHandle) == PlaybackState.Playing)
        {
            if (Bass.ChannelPause(_tempoStreamHandle))
            {
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Paused;
                StopUiUpdateTimer();
            }
            else
            {
                Debug.WriteLine($"[PlaybackService] Bass.ChannelPause failed. Error: {Bass.LastError}");
            }
        }
    }

    public void Resume()
    {
        if (!IsPlaying && CurrentSong != null)
        {
            TimeSpan resumePosition = CurrentPosition;

            // If stream is stopped or not initialized, re-initialize
            if (_tempoStreamHandle == 0 || Bass.ChannelIsActive(_tempoStreamHandle) == PlaybackState.Stopped)
            {
                try
                {
                    Debug.WriteLine($"[PlaybackService] Re-initializing stream for Resume. Position: {resumePosition}");
                    InitializeBassPipeline(CurrentSong.FilePath);
                    if (_streamHandle != 0) // Check if InitializeBassPipeline was successful
                    {
                        // Seek to the previous position in the source stream
                        long posBytes = Bass.ChannelSeconds2Bytes(_streamHandle, resumePosition.TotalSeconds);
                        Bass.ChannelSetPosition(_streamHandle, posBytes, PositionFlags.Bytes); // Set on source stream
                        CurrentPosition = resumePosition; // Update UI property
                    }
                    else
                    {
                        CleanUpPlaybackResources();
                        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PlaybackService] Exception during Resume re-initialization: {ex}");
                    CleanUpPlaybackResources();
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    return;
                }
            }

            // Play the tempo stream (which might have been paused or newly created)
            if (_tempoStreamHandle != 0)
            {
                if (Bass.ChannelPlay(_tempoStreamHandle, false)) // false = play from current position
                {
                    IsPlaying = true;
                    CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                    StartUiUpdateTimer();
                }
                else
                {
                    Debug.WriteLine($"[PlaybackService] Bass.ChannelPlay failed on Resume. Error: {Bass.LastError}");
                }
            }
        }
    }

    public void Stop()
    {
        if (_tempoStreamHandle != 0)
        {
            Bass.ChannelStop(_tempoStreamHandle);
            // Bass.ChannelSetPosition(_tempoStreamHandle, 0, PositionFlags.Bytes); // Reset position for next play
        }
        // CleanUpPlaybackResources will free the stream if FxFreeSource is used, or stop will make it inactive.
        // If we want to allow playing again from start without full re-init, don't cleanup here, just stop and reset position.
        // For full stop and release:
        CleanUpPlaybackResources();
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        CurrentPosition = TimeSpan.Zero;
    }

    public void Seek(TimeSpan positionInTrueTime)
    {
        if (_streamHandle != 0) // Seeking should be done on the source stream for tempo streams
        {
            var targetPosition = positionInTrueTime;
            if (targetPosition < TimeSpan.Zero) targetPosition = TimeSpan.Zero;

            long durationBytes = Bass.ChannelGetLength(_streamHandle, PositionFlags.Bytes);
            TimeSpan sourceDuration = TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_streamHandle, durationBytes));
            if (targetPosition > sourceDuration) targetPosition = sourceDuration;

            long seekBytes = Bass.ChannelSeconds2Bytes(_streamHandle, targetPosition.TotalSeconds);
            if (Bass.ChannelSetPosition(_streamHandle, seekBytes, PositionFlags.Bytes))
            {
                // CurrentPosition will be updated by the UI timer or can be set directly
                CurrentPosition = targetPosition;
                // If paused, the UI won't update, so update manually.
                // If playing, the next UI update will pick up the new position.
                // This also ensures CurrentPositionSeconds reflects the new value for immediate UI feedback.
                OnPropertyChanged(nameof(CurrentPositionSeconds));
            }
            else
            {
                Debug.WriteLine($"[PlaybackService] Bass.ChannelSetPosition on _streamHandle failed. Error: {Bass.LastError}");
            }
        }
    }

    private bool _isDisposed = false;
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed) return;

        if (disposing)
        {
            CleanUpPlaybackResources();
            uiUpdateTimer?.Dispose();
            uiUpdateTimer = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                NativeMethods.SetDllDirectory(null); // Restore default search path
                Debug.WriteLine("[PlaybackService Dispose] SetDllDirectory(null) called.");
            }

            if (_bassSuccessfullyInitialized)
            {
                Bass.Free(); // Free all BASS resources
                _bassSuccessfullyInitialized = false;
                Debug.WriteLine("[PlaybackService Dispose] Bass.Free called.");
            }
        }
        _isDisposed = true;
    }
}