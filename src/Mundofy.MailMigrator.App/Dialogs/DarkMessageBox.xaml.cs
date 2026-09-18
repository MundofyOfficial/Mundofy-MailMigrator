using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Mundofy.MailMigrator.App.Dialogs;

public partial class DarkMessageBox : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public DarkMessageBox(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
    {
        InitializeComponent();

        TxtTitle.Text = title;
        TxtMessage.Text = message;

        // Configure Icon
        switch (icon)
        {
            case MessageBoxImage.Warning:
                IconPath.Data = Geometry.Parse("M 12,2.5 C 12.45,2.5 12.85,2.75 13.08,3.15 L 22.33,19.4 C 22.56,19.8 22.37,20.5 21.75,20.5 L 2.25,20.5 C 1.63,20.5 1.44,19.8 1.67,19.4 L 10.92,3.15 C 11.15,2.75 11.55,2.5 12,2.5 Z M 11.25,8.5 C 11.25,8.09 11.59,7.75 12,7.75 C 12.41,7.75 12.75,8.09 12.75,8.5 L 12.75,13.25 C 12.75,13.66 12.41,14 12,14 C 11.59,14 11.25,13.66 11.25,13.25 Z M 12,17 C 12.55,17 13,16.55 13,16 C 13,15.45 12.55,15 12,15 C 11.45,15 11,15.45 11,16 C 11,16.55 11.45,17 12,17 Z");
                IconPath.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x28, 0xF5, 0x9E, 0x0B));
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06));
                break;
            case MessageBoxImage.Error:
                IconPath.Data = Geometry.Parse("M 12,2 C 6.48,2 2,6.48 2,12 C 2,17.52 6.48,22 12,22 C 17.52,22 22,17.52 22,12 C 22,6.48 17.52,2 12,2 Z M 16,9.4 L 13.4,12 L 16,14.6 L 14.6,16 L 12,13.4 L 9.4,16 L 8,14.6 L 10.6,12 L 8,9.4 L 9.4,8 L 12,10.6 L 14.6,8 Z");
                IconPath.Fill = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x28, 0xEF, 0x44, 0x44));
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                break;
            case MessageBoxImage.Question:
                IconPath.Data = Geometry.Parse("M 12,2 C 6.48,2 2,6.48 2,12 C 2,17.52 6.48,22 12,22 C 17.52,22 22,17.52 22,12 C 22,6.48 17.52,2 12,2 Z M 12,17.25 C 12.55,17.25 13,16.8 13,16.25 C 13,15.7 12.55,15.25 12,15.25 C 11.45,15.25 11,15.7 11,16.25 C 11,16.8 11.45,17.25 12,17.25 Z M 12.94,11.87 C 13.62,11.49 14,10.76 14,10 C 14,8.76 12.99,7.75 11.75,7.75 C 10.51,7.75 9.5,8.76 9.5,10 C 9.5,10.41 9.84,10.75 10.25,10.75 C 10.66,10.75 11,10.41 11,10 C 11,9.59 11.34,9.25 11.75,9.25 C 12.16,9.25 12.5,9.59 12.5,10 C 12.5,10.25 12.37,10.5 12.15,10.62 L 11.28,11.12 C 10.64,11.49 10.25,12.17 10.25,12.91 L 10.25,13.5 C 10.25,13.91 10.59,14.25 11,14.25 C 11.41,14.25 11.75,13.91 11.75,13.5 L 11.75,12.91 C 11.75,12.67 11.88,12.42 12.1,12.29 Z");
                IconPath.Fill = new SolidColorBrush(Color.FromRgb(0x81, 0x8C, 0xF8));
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x28, 0x81, 0x8C, 0xF8));
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1));
                break;
            default:
                IconPath.Data = Geometry.Parse("M 12,2 C 6.48,2 2,6.48 2,12 C 2,17.52 6.48,22 12,22 C 17.52,22 22,17.52 22,12 C 22,6.48 17.52,2 12,2 Z M 11,11 C 11,10.45 11.45,10 12,10 C 12.55,10 13,10.45 13,11 L 13,16 C 13,16.55 12.55,17 12,17 C 11.45,17 11,16.55 11,16 Z M 12,8.5 C 12.55,8.5 13,8.05 13,7.5 C 13,6.95 12.55,6.5 12,6.5 C 11.45,6.5 11,6.95 11,7.5 C 11,8.05 11.45,8.5 12,8.5 Z");
                IconPath.Fill = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
                IconBadge.Background = new SolidColorBrush(Color.FromArgb(0x28, 0x3B, 0x82, 0xF6));
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
                break;
        }

        // Configure Buttons
        BtnOk.Visibility = Visibility.Collapsed;
        BtnYes.Visibility = Visibility.Collapsed;
        BtnNo.Visibility = Visibility.Collapsed;
        BtnCancel.Visibility = Visibility.Collapsed;

        switch (buttons)
        {
            case MessageBoxButton.OK:
                BtnOk.Visibility = Visibility.Visible;
                BtnOk.IsDefault = true;
                break;
            case MessageBoxButton.OKCancel:
                BtnOk.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
                BtnOk.IsDefault = true;
                break;
            case MessageBoxButton.YesNo:
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
                BtnYes.IsDefault = true;
                break;
            case MessageBoxButton.YesNoCancel:
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;
                BtnCancel.Visibility = Visibility.Visible;
                BtnYes.IsDefault = true;
                break;
        }
    }

    public static MessageBoxResult Show(string message, string title = "Mundofy MailMigrator", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information, Window? owner = null)
    {
        var dlg = new DarkMessageBox(message, title, buttons, icon)
        {
            Owner = owner ?? Application.Current.MainWindow
        };
        dlg.ShowDialog();
        return dlg.Result;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Cancel;
        Close();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.OK;
        Close();
    }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Yes;
        Close();
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.No;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Cancel;
        Close();
    }
}
