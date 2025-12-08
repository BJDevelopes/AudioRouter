using System;
using System.Globalization;
using System.Windows.Data;

namespace AudioRouter.Converters
{
    public class VolumeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float volume)
            {
                return volume * 100;
            }
            return 100;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double sliderValue)
            {
                return (float)(sliderValue / 100.0);
            }
            return 1.0f;
        }
    }
}
