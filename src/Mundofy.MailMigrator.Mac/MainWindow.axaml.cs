using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Mundofy.MailMigrator.Mac.ViewModels;

namespace Mundofy.MailMigrator.Mac;

public partial class MainWindow : Window
{
    private readonly MacMainViewModel _vm;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        _vm = new MacMainViewModel();
        DataContext = _vm;

        var mainTabs = this.FindControl<TabControl>("MainTabs");
        if (mainTabs != null)
        {
            mainTabs.SelectionChanged += MainTabs_SelectionChanged;
        }
    }

    private void MainTabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Reset scroll position to top whenever switching tabs
        var mainTabs = this.FindControl<TabControl>("MainTabs");
        if (ReferenceEquals(e.Source, mainTabs))
        {
            this.FindControl<ScrollViewer>("SingleTabScroll")?.ScrollToHome();
            this.FindControl<ScrollViewer>("SettingsTabScroll")?.ScrollToHome();
            this.FindControl<ScrollViewer>("PrivacyTabScroll")?.ScrollToHome();
            this.FindControl<ScrollViewer>("AboutTabScroll")?.ScrollToHome();
        }
    }

    private async void StartSingleMigration_Click(object? sender, RoutedEventArgs e)
    {
        await _vm.StartSingleMigrationAsync();
    }

    private void CancelSingleMigration_Click(object? sender, RoutedEventArgs e)
    {
        _vm.CancelSingleMigration();
    }

    private async void CheckSourceQuota_Click(object? sender, RoutedEventArgs e)
    {
        await _vm.CheckSingleSourceQuotaAsync();
    }

    private async void CheckDestQuota_Click(object? sender, RoutedEventArgs e)
    {
        await _vm.CheckSingleDestQuotaAsync();
    }

    private async void AutoDetectSource_Click(object? sender, RoutedEventArgs e)
    {
        await _vm.AutoDetectSourceAsync();
    }

    private async void AutoDetectDest_Click(object? sender, RoutedEventArgs e)
    {
        await _vm.AutoDetectDestAsync();
    }

    private async void StartBatchMigration_Click(object? sender, RoutedEventArgs e)
    {
        await _vm.StartBatchMigrationAsync();
    }

    private void CancelBatchMigration_Click(object? sender, RoutedEventArgs e)
    {
        _vm.CancelBatchMigration();
    }

    private void AddAccount_Click(object? sender, RoutedEventArgs e)
    {
        _vm.AddBatchAccount();
    }

    private void RemoveAccount_Click(object? sender, RoutedEventArgs e)
    {
        _vm.RemoveSelectedAccount();
    }

    private void ClearLogs_Click(object? sender, RoutedEventArgs e)
    {
        _vm.ClearLogs();
    }

    private async void SignInSingleSourceOAuth_Click(object? sender, RoutedEventArgs e)
    {
        await _vm.SignInOAuthAsync(isSource: true);
    }

    private void ClearSingleSourceOAuth_Click(object? sender, RoutedEventArgs e)
    {
        _vm.ClearOAuth(isSource: true);
    }

    private async void SignInSingleDestOAuth_Click(object? sender, RoutedEventArgs e)
    {
        await _vm.SignInOAuthAsync(isSource: false);
    }

    private void ClearSingleDestOAuth_Click(object? sender, RoutedEventArgs e)
    {
        _vm.ClearOAuth(isSource: false);
    }

    private async void BrowseSourceServiceAccount_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Source Google Service Account JSON Key",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("JSON Key Files") { Patterns = new[] { "*.json" } },
                    new Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });
            if (files.Count > 0)
            {
                var path = files[0].Path.LocalPath;
                if (!string.IsNullOrEmpty(path))
                {
                    _vm.LoadSourceServiceAccountFile(path);
                }
            }
        }
    }

    private async void BrowseDestServiceAccount_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Destination Google Service Account JSON Key",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("JSON Key Files") { Patterns = new[] { "*.json" } },
                    new Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });
            if (files.Count > 0)
            {
                var path = files[0].Path.LocalPath;
                if (!string.IsNullOrEmpty(path))
                {
                    _vm.LoadDestServiceAccountFile(path);
                }
            }
        }
    }

    private void OpenUpdatePage_Click(object? sender, RoutedEventArgs e)
    {
        _vm.OpenUpdatePage();
    }
}
