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
}
