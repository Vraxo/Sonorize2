using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Sonorize.Utils;

public class AlbumArtistTupleComparer : IEqualityComparer<(string Album, string Artist)>
{
    public bool Equals((string Album, string Artist) x, (string Album, string Artist) y)
    {
        return string.Equals(x.Album, y.Album, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(x.Artist, y.Artist, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode([DisallowNull] (string Album, string Artist) obj)
    {
        int albumHashCode = obj.Album?.ToLowerInvariant().GetHashCode() ?? 0;
        int artistHashCode = obj.Artist?.ToLowerInvariant().GetHashCode() ?? 0;
        return HashCode.Combine(albumHashCode, artistHashCode);
    }

    public static readonly AlbumArtistTupleComparer Instance = new();
}