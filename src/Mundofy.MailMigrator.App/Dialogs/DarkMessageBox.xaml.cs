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
                IconGlyph.Text = "⚠️";
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x1A, 0x03)); // Amber/Dark
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06));
                break;
            case MessageBoxImage.Error:
                IconGlyph.Text = "⛔";
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x45, 0x0A, 0x0A)); // Red/Dark
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                break;
            case MessageBoxImage.Question:
                IconGlyph.Text = "❓";
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A)); // Blue
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
                break;
            default:
                IconGlyph.Text = "ℹ";
                IconBadge.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A));
                IconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
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
