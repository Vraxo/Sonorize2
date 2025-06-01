using System;
using Sonorize.ViewModels;

namespace Sonorize.Models;

public class LoopRegion : ViewModelBase
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

    public LoopRegion(TimeSpan start, TimeSpan end, string name = "Loop")
    {
        Name = name;
        Start = start;
        End = end;
    }
}