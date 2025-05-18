// Path: Source/Utils/AlbumArtistTupleComparer.cs (or a similar appropriate location)
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis; // For NotNullWhen attribute

namespace Sonorize.Utils; // Or your preferred namespace for utility classes

public class AlbumArtistTupleComparer : IEqualityComparer<(string Album, string Artist)>
{
    public bool Equals((string Album, string Artist) x, (string Album, string Artist) y)
    {
        return string.Equals(x.Album, y.Album, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(x.Artist, y.Artist, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode([DisallowNull] (string Album, string Artist) obj)
    {
        // Combine hash codes in a way that's sensitive to order and case-insensitivity
        // For case-insensitivity in hash code, convert to a consistent case first
        int albumHashCode = obj.Album?.ToLowerInvariant().GetHashCode() ?? 0;
        int artistHashCode = obj.Artist?.ToLowerInvariant().GetHashCode() ?? 0;
        return HashCode.Combine(albumHashCode, artistHashCode);
    }

    // Static instance for convenience
    public static readonly AlbumArtistTupleComparer Instance = new AlbumArtistTupleComparer();
}