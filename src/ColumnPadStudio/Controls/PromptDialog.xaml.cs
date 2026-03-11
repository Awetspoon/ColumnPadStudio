using System.Windows;

namespace ColumnPadStudio.Controls;

public partial class PromptDialog : Window
{
    public string DialogTitle { get; set; } = "Input";
    public string Message { get; set; } = "Enter a value:";
    public string InputText { get; set; } = "";

    public PromptDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    public static string? Show(Window owner, string title, string message, string initialValue)
    {
        var dlg = new PromptDialog
        {
            Owner = owner,
            DialogTitle = title,
            Message = message,
            InputText = initialValue
        };
        return dlg.ShowDialog() == true ? dlg.InputText : null;
    }
}
