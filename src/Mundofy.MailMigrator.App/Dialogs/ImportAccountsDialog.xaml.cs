using System.Windows;
using System.Windows.Input;
using Mundofy.MailMigrator.App.ViewModels;

namespace Mundofy.MailMigrator.App.Dialogs;

/// <summary>
/// Modern dark-mode modal dialog for deciding whether to replace or append imported accounts.
/// </summary>
public partial class ImportAccountsDialog : Window
{
    public BatchImportDecision Decision { get; private set; } = BatchImportDecision.Cancel;

    public ImportAccountsDialog(int currentCount, int incomingCount, string sourceDescription)
    {
        InitializeComponent();
        TxtCurrentCount.Text = $"{currentCount} account(s)";
        TxtIncomingCount.Text = $"{incomingCount} account(s)";
        TxtSourceDescription.Text = $"from {sourceDescription}";
    }

    public static BatchImportDecision Show(Window? owner, int currentCount, int incomingCount, string sourceDescription)
    {
        var dlg = new ImportAccountsDialog(currentCount, incomingCount, sourceDescription)
        {
            Owner = owner ?? Application.Current.MainWindow
        };
        dlg.ShowDialog();
        return dlg.Decision;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        Decision = BatchImportDecision.Replace;
        DialogResult = true;
        Close();
    }

    private void Append_Click(object sender, RoutedEventArgs e)
    {
        Decision = BatchImportDecision.Append;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Decision = BatchImportDecision.Cancel;
        DialogResult = false;
        Close();
    }
}
