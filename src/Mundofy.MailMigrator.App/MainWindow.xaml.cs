using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace Mundofy.MailMigrator.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch { }
    }

    private void OpenGitHubIssues_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/MundofyOfficial/Mundofy-MailMigrator/issues",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void OnDataGridRowPreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.DataGridRow row)
        {
            row.IsSelected = true;
            row.Focus();
        }
    }
}