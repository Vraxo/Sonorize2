using System;
using System.Runtime.InteropServices;
using NAudio.Wave;
using System;
using System.Runtime.InteropServices;

namespace VarispeedDemo.SoundTouch;

class VarispeedSampleProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider sourceProvider;
    private readonly SoundTouch soundTouch;
    private readonly float[] sourceReadBuffer;
    private readonly float[] soundTouchReadBuffer;
    private readonly int channelCount;
    private float playbackRate = 1.0f;
    private SoundTouchProfile currentSoundTouchProfile;
    private bool repositionRequested;

    public VarispeedSampleProvider(ISampleProvider sourceProvider, int readDurationMilliseconds, SoundTouchProfile soundTouchProfile)
    {
        soundTouch = new SoundTouch();
        // explore what the default values are before we change them:
        //Debug.WriteLine(String.Format("SoundTouch Version {0}", soundTouch.VersionString));
        //Debug.WriteLine("Use QuickSeek: {0}", soundTouch.GetUseQuickSeek());
        //Debug.WriteLine("Use AntiAliasing: {0}", soundTouch.GetUseAntiAliasing());

        SetSoundTouchProfile(soundTouchProfile);
        this.sourceProvider = sourceProvider;
        soundTouch.SetSampleRate(WaveFormat.SampleRate);
        channelCount = WaveFormat.Channels;
        soundTouch.SetChannels(channelCount);
        sourceReadBuffer = new float[(WaveFormat.SampleRate * channelCount * (long)readDurationMilliseconds) / 1000];
        soundTouchReadBuffer = new float[sourceReadBuffer.Length * 10]; // support down to 0.1 speed
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (playbackRate == 0) // play silence
        {
            for (int n = 0; n < count; n++)
            {
                buffer[offset++] = 0;
            }
            return count;
        }

        if (repositionRequested)
        {
            soundTouch.Clear();
            repositionRequested = false;
        }

        int samplesRead = 0;
        bool reachedEndOfSource = false;
        while (samplesRead < count)
        {
            if (soundTouch.NumberOfSamplesAvailable == 0)
            {
                var readFromSource = sourceProvider.Read(sourceReadBuffer, 0, sourceReadBuffer.Length);
                if (readFromSource > 0)
                {
                    soundTouch.PutSamples(sourceReadBuffer, readFromSource / channelCount);
                }
                else
                {
                    reachedEndOfSource = true;
                    // we've reached the end, tell SoundTouch we're done
                    soundTouch.Flush();
                }
            }
            var desiredSampleFrames = (count - samplesRead) / channelCount;

            var received = soundTouch.ReceiveSamples(soundTouchReadBuffer, desiredSampleFrames) * channelCount;
            // use loop instead of Array.Copy due to WaveBuffer
            for (int n = 0; n < received; n++)
            {
                buffer[offset + samplesRead++] = soundTouchReadBuffer[n];
            }
            if (received == 0 && reachedEndOfSource) break;
        }
        return samplesRead;
    }

    public WaveFormat WaveFormat => sourceProvider.WaveFormat;

    public float PlaybackRate
    {
        get
        {
            return playbackRate;
        }
        set
        {
            if (playbackRate != value)
            {
                UpdatePlaybackRate(value);
                playbackRate = value;
            }
        }
    }

    private void UpdatePlaybackRate(float value)
    {
        if (value != 0)
        {
            if (currentSoundTouchProfile.UseTempo)
            {
                soundTouch.SetTempo(value);
            }
            else
            {
                soundTouch.SetRate(value);
            }
        }
    }

    public void Dispose()
    {
        soundTouch.Dispose();
    }

    public void SetSoundTouchProfile(SoundTouchProfile soundTouchProfile)
    {
        if (currentSoundTouchProfile != null &&
            playbackRate != 1.0f &&
            soundTouchProfile.UseTempo != currentSoundTouchProfile.UseTempo)
        {
            if (soundTouchProfile.UseTempo)
            {
                soundTouch.SetRate(1.0f);
                soundTouch.SetPitchOctaves(0f);
                soundTouch.SetTempo(playbackRate);
            }
            else
            {
                soundTouch.SetTempo(1.0f);
                soundTouch.SetRate(playbackRate);
            }
        }
        this.currentSoundTouchProfile = soundTouchProfile;
        soundTouch.SetUseAntiAliasing(soundTouchProfile.UseAntiAliasing);
        soundTouch.SetUseQuickSeek(soundTouchProfile.UseQuickSeek);
    }

    public void Reposition()
    {
        repositionRequested = true;
    }
}

public class SoundTouchProfile
{
    public bool UseTempo { get; }
    public bool UseQuickSeek { get; }
    public bool UseAntiAliasing { get; }

    public SoundTouchProfile(bool useTempo, bool useQuickSeek = true, bool useAntiAliasing = true)
    {
        UseTempo = useTempo;
        UseQuickSeek = useQuickSeek;
        UseAntiAliasing = useAntiAliasing;
    }
}



public class SoundTouch : IDisposable
{
    private IntPtr handle;

    public SoundTouch()
    {
        handle = soundtouch_createInstance();
    }

    public void Dispose()
    {
        if (handle != IntPtr.Zero)
        {
            soundtouch_destroyInstance(handle);
            handle = IntPtr.Zero;
        }
    }

    public void SetSampleRate(int sampleRate) => soundtouch_setSampleRate(handle, sampleRate);
    public void SetChannels(int channels) => soundtouch_setChannels(handle, channels);
    public void SetTempo(float tempo) => soundtouch_setTempo(handle, tempo);
    public void SetRate(float rate) => soundtouch_setRate(handle, rate);
    public void SetPitchOctaves(float pitch) => soundtouch_setPitchOctaves(handle, pitch);
    public void SetUseQuickSeek(bool useQuickSeek) => soundtouch_setSetting(handle, 0, useQuickSeek ? 1 : 0);
    public void SetUseAntiAliasing(bool useAntiAliasing) => soundtouch_setSetting(handle, 1, useAntiAliasing ? 1 : 0);
    public void PutSamples(float[] samples, int numSamples) => soundtouch_putSamples(handle, samples, numSamples);
    public int ReceiveSamples(float[] samples, int maxSamples) => soundtouch_receiveSamples(handle, samples, maxSamples);
    public int NumberOfSamplesAvailable => soundtouch_numSamples(handle);
    public void Flush() => soundtouch_flush(handle);
    public void Clear() => soundtouch_clear(handle);

    // Native bindings
    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr soundtouch_createInstance();

    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_destroyInstance(IntPtr handle);

    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_setSampleRate(IntPtr handle, int srate);

    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_setChannels(IntPtr handle, int numChannels);

    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_setTempo(IntPtr handle, float newTempo);

    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_setRate(IntPtr handle, float newRate);

    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_setPitchOctaves(IntPtr handle, float newPitch);

    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_setSetting(IntPtr handle, int settingId, int value);

    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_putSamples(IntPtr handle, float[] samples, int numSamples);

    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern int soundtouch_receiveSamples(IntPtr handle, float[] outBuffer, int maxSamples);

    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern int soundtouch_numSamples(IntPtr handle);

    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_flush(IntPtr handle);

    [DllImport("SoundTouch", CallingConvention = CallingConvention.Cdecl)]
    private static extern void soundtouch_clear(IntPtr handle);
}
