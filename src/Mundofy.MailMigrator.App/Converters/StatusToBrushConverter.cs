using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Mundofy.MailMigrator.Core.Models;

namespace Mundofy.MailMigrator.App.Converters;

public class StatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Green = new((Color)ColorConverter.ConvertFromString("#10B981"));
    private static readonly SolidColorBrush Blue = new((Color)ColorConverter.ConvertFromString("#3B82F6"));
    private static readonly SolidColorBrush Red = new((Color)ColorConverter.ConvertFromString("#EF4444"));
    private static readonly SolidColorBrush Yellow = new((Color)ColorConverter.ConvertFromString("#F59E0B"));
    private static readonly SolidColorBrush Gray = new((Color)ColorConverter.ConvertFromString("#6B7280"));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MigrationStatus status)
        {
            return status switch
            {
                MigrationStatus.Completed => Green,
                MigrationStatus.Ready => Green,
                MigrationStatus.InProgress => Blue,
                MigrationStatus.Testing => Yellow,
                MigrationStatus.Failed => Red,
                MigrationStatus.Paused => Yellow,
                _ => Gray
            };
        }
        return Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
