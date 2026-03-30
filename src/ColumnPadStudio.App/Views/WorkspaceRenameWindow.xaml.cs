using System.ComponentModel;
using System.Windows;

namespace ColumnPadStudio.App.Views;

public partial class WorkspaceRenameWindow : Window, INotifyPropertyChanged
{
    private string _workspaceName;

    public WorkspaceRenameWindow(string currentName)
    {
        InitializeComponent();
        _workspaceName = currentName;
        DataContext = this;
        Loaded += (_, _) =>
        {
            WorkspaceNameTextBox.Focus();
            WorkspaceNameTextBox.SelectAll();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string WorkspaceName
    {
        get => _workspaceName;
        set
        {
            if (_workspaceName == value)
            {
                return;
            }

            _workspaceName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WorkspaceName)));
        }
    }

    private void OnRenameClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(WorkspaceName))
        {
            MessageBox.Show("Enter a workspace name.", "Rename Workspace", MessageBoxButton.OK, MessageBoxImage.Information);
            WorkspaceNameTextBox.Focus();
            WorkspaceNameTextBox.SelectAll();
            return;
        }

        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
