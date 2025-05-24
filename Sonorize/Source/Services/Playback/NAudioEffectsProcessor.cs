using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using SoundTouch.Net.NAudioSupport;
using System.Diagnostics;
using System;

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
            if (_outputProvider == null)
            {
                throw new InvalidOperationException("Audio effects processor has not been initialized.");
            }
            return _outputProvider;
        }
    }

    private float _tempo = 1.0f;
    public float Tempo
    {
        get => _tempo;
        set
        {
            if (Math.Abs(_tempo - value) > float.Epsilon)
            {
                _tempo = value;
                if (_soundTouch != null)
                {
                    try
                    {
                        _soundTouch.Tempo = _tempo;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[EffectsProcessor] Error setting SoundTouch Tempo: {ex.Message}");
                    }
                }
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
                if (_pitchShifter != null)
                {
                    try
                    {
                        _pitchShifter.PitchFactor = (float)Math.Pow(2, _pitchSemitones / 12.0);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[EffectsProcessor] Error setting PitchShifter PitchFactor: {ex.Message}");
                    }
                }
            }
        }
    }

    public NAudioEffectsProcessor()
    {
    }

    public void Initialize(ISampleProvider sourceProvider)
    {
        Dispose(disposing: true);

        if (sourceProvider == null)
        {
            throw new ArgumentNullException(nameof(sourceProvider));
        }

        try
        {
            ISampleProvider monoSampleProvider = sourceProvider.ToMono();

            _sampleToWaveProvider = new SampleToWaveProvider(monoSampleProvider);

            _soundTouch = new SoundTouchWaveProvider(_sampleToWaveProvider);
            _soundTouch.Tempo = _tempo;
            _soundTouch.Rate = 1.0f;
            _soundTouch.Pitch = 1.0f;

            ISampleProvider soundTouchAsSampleProvider = _soundTouch.ToSampleProvider();

            _pitchShifter = new SmbPitchShiftingSampleProvider(soundTouchAsSampleProvider);
            _pitchShifter.PitchFactor = (float)Math.Pow(2, _pitchSemitones / 12.0);

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
        if (disposing)
        {
            _sampleToWaveProvider = null;
            _soundTouch = null;
            _pitchShifter = null;
            _outputProvider = null;
        }
    }

    ~NAudioEffectsProcessor()
    {
        Dispose(disposing: false);
    }
}