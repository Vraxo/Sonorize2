using System;
using System.Diagnostics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundTouch.Net.NAudioSupport;

namespace Sonorize.Services;

public class NAudioEffectsProcessor : IDisposable
{
    private SoundTouchWaveProvider? _soundTouch;
    private SmbPitchShiftingSampleProvider? _pitchShifter;
    private SampleToWaveProvider? _sampleToWaveProvider;

    private ISampleProvider? _outputProvider;

    public ISampleProvider OutputProvider
    {
        get
        {
            if (_outputProvider is null)
            {
                throw new InvalidOperationException("Audio effects processor has not been initialized.");
            }

            return _outputProvider;
        }
    }

    public float Tempo
    {
        get;
        set
        {
            if (float.Abs(field - value) <= float.Epsilon)
            {
                return;
            }

            field = value;

            if (_soundTouch is null)
            {
                return;
            }

            try
            {
                _soundTouch.Tempo = field;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EffectsProcessor] Error setting SoundTouch Tempo: {ex.Message}");
            }
        }
    } = 1.0f;

    public float PitchSemitones
    {
        get;
        set
        {
            if (float.Abs(field - value) <= float.Epsilon)
            {
                return;
            }

            field = value;

            if (_pitchShifter is null)
            {
                return;
            }
            try
            {
                _pitchShifter.PitchFactor = (float)Math.Pow(2, field / 12.0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EffectsProcessor] Error setting PitchShifter PitchFactor: {ex.Message}");
            }
        }
    } = 0f;

    public NAudioEffectsProcessor()
    {
    }

    public void Initialize(ISampleProvider sourceProvider)
    {
        Dispose(disposing: true);

        ArgumentNullException.ThrowIfNull(sourceProvider);

        try
        {
            ISampleProvider monoSampleProvider = sourceProvider.ToMono();

            _sampleToWaveProvider = new SampleToWaveProvider(monoSampleProvider);

            _soundTouch = new SoundTouchWaveProvider(_sampleToWaveProvider);
            _soundTouch.Tempo = Tempo;
            _soundTouch.Rate = 1.0f;
            _soundTouch.Pitch = 1.0f;

            ISampleProvider soundTouchAsSampleProvider = _soundTouch.ToSampleProvider();

            _pitchShifter = new SmbPitchShiftingSampleProvider(soundTouchAsSampleProvider);
            _pitchShifter.PitchFactor = (float)Math.Pow(2, PitchSemitones / 12.0);

            _outputProvider = _pitchShifter;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EffectsProcessor] CRITICAL ERROR during effects pipeline initialization: {ex.ToString()}");
            Dispose(disposing: true);
            _outputProvider = null;
            throw;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _sampleToWaveProvider = null;
        _soundTouch = null;
        _pitchShifter = null;
        _outputProvider = null;
    }

    ~NAudioEffectsProcessor()
    {
        Dispose(disposing: false);
    }
}