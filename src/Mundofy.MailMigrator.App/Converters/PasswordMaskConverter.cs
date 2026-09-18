using System.Globalization;
using System.Windows.Data;

namespace Mundofy.MailMigrator.App.Converters;

public class PasswordMaskConverter : IMultiValueConverter, IValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length > 0 && values[0] is string password)
        {
            bool mask = true;
            if (values.Length > 1 && values[1] is bool isMasked)
            {
                mask = isMasked;
            }

            if (mask && !string.IsNullOrEmpty(password))
            {
                return new string('•', Math.Clamp(password.Length, 6, 12));
            }

            return password;
        }

        return "";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string password && !string.IsNullOrEmpty(password))
        {
            return new string('•', Math.Clamp(password.Length, 6, 12));
        }

        return value ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}
