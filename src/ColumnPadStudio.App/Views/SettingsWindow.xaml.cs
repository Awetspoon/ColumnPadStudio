using ColumnPadStudio.App.ViewModels;

namespace ColumnPadStudio.App.Views;

public partial class SettingsWindow : System.Windows.Window
{
    public SettingsWindow(ShellViewModel shell)
    {
        InitializeComponent();
        DataContext = shell;
    }

    private void OnCloseClick(object sender, System.Windows.RoutedEventArgs e)
    {
        Close();
    }
}
