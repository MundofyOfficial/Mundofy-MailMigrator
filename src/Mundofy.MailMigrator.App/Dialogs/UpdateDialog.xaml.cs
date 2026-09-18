using System.Windows;
using System.Windows.Input;
using Mundofy.MailMigrator.App.Services;

namespace Mundofy.MailMigrator.App.Dialogs;

public partial class UpdateDialog : Window
{
    private readonly UpdateInfo _updateInfo;
    private CancellationTokenSource? _downloadCts;

    public UpdateDialog(UpdateInfo updateInfo)
    {
        InitializeComponent();
        _updateInfo = updateInfo;

        TxtHeading.Text = $"Update Available: v{updateInfo.LatestVersion}";
        TxtCurrentVersion.Text = $"v{updateInfo.CurrentVersion}";
        TxtLatestVersion.Text = $"v{updateInfo.LatestVersion}";
        TxtReleaseNotes.Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes) ? "No detailed release notes provided." : updateInfo.ReleaseNotes;
    }

    public static void ShowDialog(Window? owner, UpdateInfo updateInfo)
    {
        var dlg = new UpdateDialog(updateInfo)
        {
            Owner = owner ?? Application.Current.MainWindow
        };
        dlg.ShowDialog();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_updateInfo.DownloadUrl))
        {
            DarkMessageBox.Show("No direct binary download URL was found for this release. Please download it from GitHub releases.", "Download Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning, this);
            return;
        }

        BtnUpdate.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        TxtProgressStatus.Text = "Downloading update from GitHub...";

        _downloadCts = new CancellationTokenSource();
        var progress = new Progress<double>(p =>
        {
            ProgressBar.Value = p;
            TxtProgressPercent.Text = $"{p:0}%";
        });

        try
        {
            await UpdateService.DownloadAndApplyUpdateAsync(_updateInfo.DownloadUrl, progress, _downloadCts.Token);
        }
        catch (Exception ex)
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            BtnUpdate.IsEnabled = true;
            DarkMessageBox.Show($"Failed to apply update: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error, this);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _downloadCts?.Cancel();
        Close();
    }
}
