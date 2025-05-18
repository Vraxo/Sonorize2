using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sonorize.Models;

public class LoopRegion(TimeSpan start, TimeSpan end, string name = "Loop") : INotifyPropertyChanged
{
    private string _name = name; // Name field remains, but not prominently used in UI
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private TimeSpan _start = start;
    public TimeSpan Start
    {
        get => _start;
        set => SetProperty(ref _start, value);
    }

    private TimeSpan _end = end;
    public TimeSpan End
    {
        get => _end;
        set => SetProperty(ref _end, value);
    }

    // DisplayText no longer includes the name by default for main UI, focused on times
    public string DisplayText => $"({Start:mm\\:ss} - {End:mm\\:ss})";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}