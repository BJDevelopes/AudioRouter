using System;
using System.Globalization;
using System.Windows.Data;
using AudioRouter.Models;

namespace AudioRouter.Converters
{
    public class LatencyModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LatencyMode mode)
            {
                return LatencyConfiguration.GetConfiguration(mode).ToString();
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
