using System;
using System.Globalization;
using System.Windows.Data;

namespace AudioRouter.Converters
{
    public class AudioLevelWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is float audioLevel && values[1] is double containerWidth)
            {
                // AudioLevel is 0-1, multiply by container width
                return Math.Max(2, audioLevel * (containerWidth - 4)); // Min width of 2, subtract padding
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
