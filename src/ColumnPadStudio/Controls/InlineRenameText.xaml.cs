using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColumnPadStudio.Controls;

public partial class InlineRenameText : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(InlineRenameText),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IsEditingProperty =
        DependencyProperty.Register(
            nameof(IsEditing),
            typeof(bool),
            typeof(InlineRenameText),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsEditingChanged));

    private string _beforeEditText = string.Empty;
    private bool _suppressLostFocusCommit;
    private bool _selectAllOnFocus;

    public InlineRenameText()
    {
        InitializeComponent();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsEditing
    {
        get => (bool)GetValue(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    private static void OnIsEditingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not InlineRenameText control)
            return;

        var next = (bool)e.NewValue;
        if (next)
            control.BeginEditInternal();
        else
            control.EndEditVisual();
    }

    private void BeginEditInternal()
    {
        _beforeEditText = Text ?? string.Empty;
        _selectAllOnFocus = true;

        DisplayTextBlock.Visibility = Visibility.Collapsed;
        EditorTextBox.Visibility = Visibility.Visible;

        // Use ContextIdle so the initial mouse-up from opening rename cannot move
        // the caret to the edge before we grab focus/select-all.
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            EditorTextBox.Focus();
        }));
    }

    private void EndEditVisual()
    {
        _selectAllOnFocus = false;
        EditorTextBox.Visibility = Visibility.Collapsed;
        DisplayTextBlock.Visibility = Visibility.Visible;
    }

    private void EditorTextBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (EditorTextBox.IsKeyboardFocusWithin)
            return;

        _selectAllOnFocus = true;
        e.Handled = true;
        EditorTextBox.Focus();
    }

    private void EditorTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_selectAllOnFocus)
            return;

        EditorTextBox.SelectAll();
        _selectAllOnFocus = false;
    }

    private void EditorTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitEdit();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            CancelEdit();
            e.Handled = true;
        }
    }

    private void EditorTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_suppressLostFocusCommit || !IsEditing)
            return;

        CommitEdit();
    }

    private void CommitEdit()
    {
        var candidate = EditorTextBox.Text?.Trim();
        Text = string.IsNullOrWhiteSpace(candidate) ? _beforeEditText : candidate;
        IsEditing = false;
    }

    private void CancelEdit()
    {
        _suppressLostFocusCommit = true;
        try
        {
            Text = _beforeEditText;
            IsEditing = false;
        }
        finally
        {
            _suppressLostFocusCommit = false;
        }
    }
}
