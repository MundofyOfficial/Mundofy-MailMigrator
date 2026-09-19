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

    private void OpenGitHubRepo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/MundofyOfficial/Mundofy-MailMigrator",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void SwitchToPrivacyTab_Click(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = 3;
    }

    private bool _isSwitchingTab;

    private void MainTabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, MainTabControl))
        {
            _isSwitchingTab = true;
            ResetSelectedTabScroll();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
            {
                ResetSelectedTabScroll();
                _isSwitchingTab = false;
            }));
        }
    }

    private void TabScrollViewer_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
    {
        if (_isSwitchingTab)
        {
            e.Handled = true;
        }
    }

    private void ResetSelectedTabScroll()
    {
        if (MainTabControl.SelectedContent is System.Windows.Controls.ScrollViewer sv)
        {
            sv.ScrollToVerticalOffset(0);
            sv.ScrollToHorizontalOffset(0);
            sv.ScrollToTop();
        }
        else if (MainTabControl.SelectedContent is DependencyObject dep)
        {
            var childSv = FindDescendant<System.Windows.Controls.ScrollViewer>(dep);
            if (childSv != null)
            {
                childSv.ScrollToVerticalOffset(0);
                childSv.ScrollToHorizontalOffset(0);
                childSv.ScrollToTop();
            }
        }
    }

    private void OnDataGridRowPreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.DataGridRow row)
        {
            row.IsSelected = true;
            row.Focus();
        }
    }

    private void LogListBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox listBox)
        {
            var scrollViewer = FindDescendant<System.Windows.Controls.ScrollViewer>(listBox);
            if (scrollViewer != null)
            {
                if ((e.Delta < 0 && scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight) ||
                    (e.Delta > 0 && scrollViewer.VerticalOffset <= 0))
                {
                    e.Handled = true;
                    var eventArg = new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                    {
                        RoutedEvent = UIElement.MouseWheelEvent,
                        Source = sender
                    };
                    (listBox.Parent as UIElement)?.RaiseEvent(eventArg);
                }
            }
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var descendant = FindDescendant<T>(child);
            if (descendant != null) return descendant;
        }
        return null;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.SaveCurrentSettings();
        }
    }
}