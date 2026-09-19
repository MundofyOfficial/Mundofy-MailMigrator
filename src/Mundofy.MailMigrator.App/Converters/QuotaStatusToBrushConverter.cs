using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Mundofy.MailMigrator.Core.Models;

namespace Mundofy.MailMigrator.App.Converters;

public class QuotaStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Green = new((Color)ColorConverter.ConvertFromString("#10B981"));
    private static readonly SolidColorBrush Blue = new((Color)ColorConverter.ConvertFromString("#38BDF8"));
    private static readonly SolidColorBrush Yellow = new((Color)ColorConverter.ConvertFromString("#F59E0B"));
    private static readonly SolidColorBrush Red = new((Color)ColorConverter.ConvertFromString("#EF4444"));
    private static readonly SolidColorBrush Gray = new((Color)ColorConverter.ConvertFromString("#94A3B8"));

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MailboxQuotaInfo quota)
        {
            return quota.StatusLevel switch
            {
                QuotaStatusLevel.Normal => Green,
                QuotaStatusLevel.Warning => Yellow,
                QuotaStatusLevel.Critical => Red,
                QuotaStatusLevel.Exceeded => Red,
                QuotaStatusLevel.Unlimited => Blue,
                _ => Gray
            };
        }
        if (value is QuotaStatusLevel level)
        {
            return level switch
            {
                QuotaStatusLevel.Normal => Green,
                QuotaStatusLevel.Warning => Yellow,
                QuotaStatusLevel.Critical => Red,
                QuotaStatusLevel.Exceeded => Red,
                QuotaStatusLevel.Unlimited => Blue,
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
