using System;

namespace Sonorize.Models;

public record LoopStorageData(TimeSpan Start, TimeSpan End, bool IsActive);