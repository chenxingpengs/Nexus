using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Nexus.Converters
{
    public class BoolToStatusTextConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isConfigured)
            {
                return isConfigured ? "已配置" : "待配置";
            }
            return "待配置";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToStatusColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isConfigured)
            {
                return isConfigured ? new SolidColorBrush(Color.Parse("#4CAF50")) : new SolidColorBrush(Color.Parse("#FF9800"));
            }
            return new SolidColorBrush(Color.Parse("#FF9800"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MissingToBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isMissing)
            {
                return isMissing ? new SolidColorBrush(Color.Parse("#FFF8E1")) : new SolidColorBrush(Color.Parse("#FFFFFF"));
            }
            return new SolidColorBrush(Color.Parse("#FFFFFF"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
