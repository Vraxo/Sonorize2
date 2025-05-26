using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sonorize.Models;

public class LoopRegion : INotifyPropertyChanged
{
    public string Name
    {
        get;

        set
        {
            SetProperty(ref field, value);
        }
    }

    public TimeSpan Start
    {
        get;

        set
        {
            SetProperty(ref field, value);
        }
    }

    public TimeSpan End { get; set; }

    public string DisplayText => $"({Start:mm\\:ss} - {End:mm\\:ss})";

    public event PropertyChangedEventHandler? PropertyChanged;

    public LoopRegion(TimeSpan start, TimeSpan end, string name = "Loop")
    {
        Name = name;
        Start = start;
        End = end;
    }

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