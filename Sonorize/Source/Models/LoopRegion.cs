using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sonorize.Models;

public class LoopRegion(TimeSpan start, TimeSpan end, string name = "Loop") : INotifyPropertyChanged
{
    private TimeSpan end = end;

    public string Name
    {
        get;

        set
        {
            SetProperty(ref field, value);
        }
    } = name;

    public TimeSpan Start
    {
        get;
        
        set
        {
            SetProperty(ref field, value);
        }
    } = start;

    public TimeSpan End
    {
        get => end;
        set => end = value;
    }

    public string DisplayText => $"({Start:mm\\:ss} - {End:mm\\:ss})";

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }
}