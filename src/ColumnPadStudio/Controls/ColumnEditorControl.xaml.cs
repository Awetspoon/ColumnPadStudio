using ColumnPadStudio.ViewModels;
using System.Windows.Controls;

namespace ColumnPadStudio.Controls;

public sealed class ColumnResizeDeltaEventArgs(double horizontalChange) : EventArgs
{
    public double HorizontalChange { get; } = horizontalChange;
}

public sealed class ColumnImageEventArgs(ColumnImageViewModel image) : EventArgs
{
    public ColumnImageViewModel Image { get; } = image;
}

public sealed class ColumnImageFileEventArgs(string filePath, double left, double top) : EventArgs
{
    public string FilePath { get; } = filePath;
    public double Left { get; } = left;
    public double Top { get; } = top;
}

public partial class ColumnEditorControl : UserControl
{
    public event EventHandler? EditorFocused;
    public event EventHandler? LockWidthRequested;
    public event EventHandler? MoveLeftRequested;
    public event EventHandler? MoveRightRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? ResetWidthRequested;
    public event EventHandler? ResizeRequested;
    public event EventHandler<ColumnResizeDeltaEventArgs>? RightEdgeResizeDeltaRequested;
    public event EventHandler? InsertImageRequested;
    public event EventHandler<ColumnImageFileEventArgs>? ImageFileDropped;
    public event EventHandler<ColumnImageEventArgs>? RemoveImageRequested;
    public event EventHandler? SetFontFamilyRequested;
    public event EventHandler? IncreaseFontRequested;
    public event EventHandler? DecreaseFontRequested;
    public event EventHandler? ToggleBoldRequested;
    public event EventHandler? ToggleItalicRequested;
    public event EventHandler? ResetFontRequested;

    private ScrollViewer? _editorScrollViewer;
    private bool _lineNumberRefreshPending;
    private int _lastRenderedLineNumberCount = -1;
    private int _gutterContextLineIndex = -1;
    private int _editorContextMenuCharacterIndex = -1;
    private ColumnViewModel? _observedVm;

    public ColumnEditorControl()
    {
        InitializeComponent();
        Loaded += ColumnEditorControl_Loaded;
        Unloaded += ColumnEditorControl_Unloaded;
        DataContextChanged += ColumnEditorControl_DataContextChanged;
    }

    public int SelectionStart => Editor.SelectionStart;
    public int SelectionLength => Editor.SelectionLength;
    public double PictureSurfaceWidth => ImageOverlay.ActualWidth;
    public double PictureSurfaceHeight => ImageOverlay.ActualHeight;

    private ColumnViewModel? VM => DataContext as ColumnViewModel;

    public void FocusEditor()
    {
        Editor.Focus();
        Editor.CaretIndex = Math.Clamp(Editor.CaretIndex, 0, Editor.Text.Length);
    }

    public void FocusAndSelectRange(int start, int length)
    {
        var textLength = Editor.Text.Length;
        var safeStart = Math.Clamp(start, 0, textLength);
        var safeLength = Math.Clamp(length, 0, textLength - safeStart);

        Editor.Focus();
        Editor.Select(safeStart, safeLength);
        var line = Editor.GetLineIndexFromCharacterIndex(safeStart);
        Editor.ScrollToLine(line);
    }

    public void ShowGutterBullets() => SetLineMarkerMode(LineMarkerMode.Bullets);
    public void ShowGutterChecklist() => SetLineMarkerMode(LineMarkerMode.Checklist);
    public void ToggleChecklistChecksInSelection() => ToggleChecklistChecksForSelection();

    public bool ClearSelection(bool focusEditor = true)
    {
        if (Editor.SelectionLength <= 0)
            return false;

        var caretIndex = Editor.SelectionStart + Editor.SelectionLength;
        Editor.Select(caretIndex, 0);
        if (focusEditor)
            Editor.Focus();

        return true;
    }
}
