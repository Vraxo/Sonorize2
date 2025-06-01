using System;

namespace Sonorize.Models;

public sealed record LoopRegion(TimeSpan Start, TimeSpan End, string Name = "Loop")
{
    public string DisplayText => $"({Start:mm\\:ss} - {End:mm\\:ss})";
}