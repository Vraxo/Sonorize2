using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Sonorize.ViewModels;

namespace Sonorize.Converters
{
    public class GridViewImageVisibilityConverter : IMultiValueConverter
    {
        public GridViewImageType TargetType { get; set; }

        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count != 2 || values[1] is not GridViewImageType currentSetting)
            {
                return false;
            }

            int thumbnailCount = 0;
            if (values[0] is IEnumerable<Bitmap?> thumbnails)
            {
                thumbnailCount = thumbnails.Count(t => t is not null);
            }

            if (TargetType == GridViewImageType.Composite)
            {
                return currentSetting == GridViewImageType.Composite && thumbnailCount > 1;
            }
            else // TargetType is Single
            {
                return currentSetting == GridViewImageType.Single || (currentSetting == GridViewImageType.Composite && thumbnailCount <= 1);
            }
        }
    }
}
