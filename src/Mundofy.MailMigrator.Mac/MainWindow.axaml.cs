using Avalonia.Controls;
using Avalonia.Interactivity;
using Mundofy.MailMigrator.Mac.ViewModels;

namespace Mundofy.MailMigrator.Mac;

public partial class MainWindow : Window
{
    private readonly MacMainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MacMainViewModel();
        DataContext = _vm;

        MainTabs.SelectionChanged += MainTabs_SelectionChanged;
    }

    private void MainTabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Reset scroll position to top whenever switching tabs
        if (ReferenceEquals(e.Source, MainTabs))
        {
            SingleTabScroll?.ScrollToHome();
            SettingsTabScroll?.ScrollToHome();
            PrivacyTabScroll?.ScrollToHome();
            AboutTabScroll?.ScrollToHome();
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
