using System.Windows;

namespace ColumnPadStudio.App.Views;

public partial class FindReplaceWindow : Window
{
    public FindReplaceWindow(string searchText, string replaceText, bool showReplaceField)
    {
        InitializeComponent();
        SearchText = searchText;
        ReplaceText = replaceText;
        ShowReplaceField = showReplaceField;
        DataContext = this;

        Title = showReplaceField ? "Replace All" : "Find";
        ReplaceLabel.Visibility = showReplaceField ? Visibility.Visible : Visibility.Collapsed;
        ReplaceTextBox.Visibility = showReplaceField ? Visibility.Visible : Visibility.Collapsed;
        if (!showReplaceField)
        {
            Height = 180;
            HintTextBlock.Text = "Find searches the active workspace across all columns and selects the next match.";
        }

        Loaded += (_, _) => FindTextBox.Focus();
    }

    public string SearchText { get; set; }

    public string ReplaceText { get; set; }

    public bool ShowReplaceField { get; }

    private void OnAcceptClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
