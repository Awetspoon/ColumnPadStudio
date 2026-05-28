using System.Windows;
using System.Windows.Controls;

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
        Loaded += PromptDialog_Loaded;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void PromptDialog_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= PromptDialog_Loaded;

        if (InputComboBox.Visibility == Visibility.Visible)
            InputComboBox.Focus();
        else
            InputTextBox.Focus();
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

    public static string? ShowChoice(Window owner, string title, string message, string initialValue, IEnumerable<string> choices)
    {
        var dlg = new PromptDialog
        {
            Owner = owner,
            DialogTitle = title,
            Message = message,
            InputText = initialValue
        };

        var options = choices
            .Where(choice => !string.IsNullOrWhiteSpace(choice))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(choice => choice, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (!options.Contains(initialValue, StringComparer.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(initialValue))
        {
            options.Insert(0, initialValue.Trim());
        }

        dlg.InputTextBox.Visibility = Visibility.Collapsed;
        dlg.InputComboBox.Visibility = Visibility.Visible;
        dlg.InputComboBox.ItemsSource = options;
        dlg.InputComboBox.SelectedItem = options.FirstOrDefault(option => string.Equals(option, initialValue, StringComparison.OrdinalIgnoreCase))
                                        ?? options.FirstOrDefault();

        if (dlg.InputComboBox.SelectedItem is string selected)
            dlg.InputText = selected;

        return dlg.ShowDialog() == true ? dlg.InputText : null;
    }
}
