using ColumnPadStudio.ViewModels;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColumnPadStudio.Controls;

public partial class ColumnEditorControl
{
    private void ColumnEditorControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SetObservedViewModel(e.NewValue as ColumnViewModel);

        _lastRenderedLineNumberCount = -1;
        _lastRenderedLineMarkerMode = null;
        _lastRenderedGutterStateVersion = -1;
        _lastRenderedChecklistLayoutVersion = -1;
        QueueLineNumberRefresh();
        SyncLineNumberScrollWithEditor();
    }

    private void ObservedVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ColumnViewModel.GutterStateVersion)
            or nameof(ColumnViewModel.ShowLineNumbers)
            or nameof(ColumnViewModel.WordWrap)
            or nameof(ColumnViewModel.EditorFontFamily)
            or nameof(ColumnViewModel.EditorFontSize)
            or nameof(ColumnViewModel.EditorFontStyle)
            or nameof(ColumnViewModel.EditorFontWeight))
        {
            _checklistLayoutVersion++;
            QueueLineNumberRefresh();
        }
    }

    private void Editor_GotFocus(object sender, RoutedEventArgs e)
    {
        VM?.DeselectImages();
        EditorFocused?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnEditorControl_Loaded(object sender, RoutedEventArgs e)
    {
        SetObservedViewModel(VM);
        AttachEditorScrollViewer();
        QueueEditorScrollRestore();
        QueueLineNumberRefresh();
        SyncLineNumberScrollWithEditor();
    }

    private void ColumnEditorControl_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachEditorScrollViewer();
        DetachObservedViewModel();
    }

    private void SetObservedViewModel(ColumnViewModel? viewModel)
    {
        if (!ReferenceEquals(_observedVm, viewModel))
        {
            DetachObservedViewModel();
            _observedVm = viewModel;
        }

        if (!IsLoaded || _observedVm is null || _isObservedVmSubscribed)
            return;

        _observedVm.PropertyChanged += ObservedVm_PropertyChanged;
        _isObservedVmSubscribed = true;
    }

    private void DetachObservedViewModel()
    {
        if (_isObservedVmSubscribed && _observedVm is not null)
            _observedVm.PropertyChanged -= ObservedVm_PropertyChanged;

        _isObservedVmSubscribed = false;
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        _checklistLayoutVersion++;
        QueueLineNumberRefresh();
        SyncLineNumberScrollWithEditor();
    }

    private void Editor_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _checklistLayoutVersion++;
        QueueLineNumberRefresh();
        SyncLineNumberScrollWithEditor();
    }

    private void AttachEditorScrollViewer()
    {
        if (_editorScrollViewer is not null)
            return;

        _editorScrollViewer = FindDescendant<ScrollViewer>(Editor);
        if (_editorScrollViewer is null)
            return;

        _editorScrollViewer.ScrollChanged += EditorScrollViewer_ScrollChanged;
        QueueEditorScrollRestore();
    }

    private void DetachEditorScrollViewer()
    {
        if (_editorScrollViewer is null)
            return;

        if (!_hasSavedEditorScrollOffsets)
        {
            _savedEditorHorizontalOffset = _editorScrollViewer.HorizontalOffset;
            _savedEditorVerticalOffset = _editorScrollViewer.VerticalOffset;
            _hasSavedEditorScrollOffsets = true;
        }

        _editorScrollViewer.ScrollChanged -= EditorScrollViewer_ScrollChanged;
        _editorScrollViewer = null;
    }

    private void QueueEditorScrollRestore()
    {
        if (!_hasSavedEditorScrollOffsets
            || _editorScrollRestorePending
            || _editorScrollViewer is null)
        {
            return;
        }

        _editorScrollRestorePending = true;
        var scrollViewer = _editorScrollViewer;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _editorScrollRestorePending = false;
            if (!IsLoaded || !ReferenceEquals(scrollViewer, _editorScrollViewer))
                return;

            scrollViewer.ScrollToHorizontalOffset(_savedEditorHorizontalOffset);
            scrollViewer.ScrollToVerticalOffset(_savedEditorVerticalOffset);
            _hasSavedEditorScrollOffsets = false;
            SyncLineNumberScroll(scrollViewer.VerticalOffset);
        }), DispatcherPriority.Loaded);
    }

    private void EditorScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange == 0 && e.ExtentHeightChange == 0)
            return;

        if (e.ExtentHeightChange != 0)
            QueueLineNumberRefresh();

        SyncLineNumberScroll(e.VerticalOffset);
    }

    private void SyncLineNumberScrollWithEditor()
    {
        AttachEditorScrollViewer();
        SyncLineNumberScroll(_editorScrollViewer?.VerticalOffset ?? 0);
    }

    private void SyncLineNumberScroll(double verticalOffset)
    {
        var safeVerticalOffset = double.IsFinite(verticalOffset) ? Math.Max(0, verticalOffset) : 0;
        LineNumbersTransform.Y = -safeVerticalOffset;
        EditorPaperBackground.VerticalOffset = safeVerticalOffset;
        LineNumberPaperBackground.VerticalOffset = safeVerticalOffset;
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;

            var nested = FindDescendant<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private void QueueLineNumberRefresh()
    {
        if (_lineNumberRefreshPending)
            return;

        _lineNumberRefreshPending = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _lineNumberRefreshPending = false;
            RefreshVisibleLineNumbers();
        }), DispatcherPriority.Background);
    }

    private void RefreshVisibleLineNumbers()
    {
        var lineCount = Math.Max(1, Editor.LineCount);
        var markerMode = VM?.LineMarkerMode ?? LineMarkerMode.Numbers;
        var gutterStateVersion = VM?.GutterStateVersion ?? 0;
        VM?.SetVisibleLineCount(lineCount);

        if (lineCount == _lastRenderedLineNumberCount
            && markerMode == _lastRenderedLineMarkerMode
            && gutterStateVersion == _lastRenderedGutterStateVersion
            && (markerMode != LineMarkerMode.Checklist
                || _checklistLayoutVersion == _lastRenderedChecklistLayoutVersion))
        {
            SyncLineNumberScrollWithEditor();
            return;
        }

        var lineBreak = Environment.NewLine;
        var sb = new StringBuilder(lineCount * (lineBreak.Length + 3));
        var visualToLogicalLines = markerMode == LineMarkerMode.Checklist
            ? BuildVisualToLogicalLineMap(lineCount)
            : null;
        for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            if (lineIndex > 0)
                sb.Append(lineBreak);

            sb.Append(GetLineNumberLabel(markerMode, lineIndex, visualToLogicalLines));
        }

        var renderedLineNumbers = sb.ToString();
        LineNumbers.Text = renderedLineNumbers;
        _lastRenderedLineNumberCount = lineCount;
        _lastRenderedLineMarkerMode = markerMode;
        _lastRenderedGutterStateVersion = gutterStateVersion;
        _lastRenderedChecklistLayoutVersion = _checklistLayoutVersion;
        SyncLineNumberScrollWithEditor();
    }

    private string GetLineNumberLabel(
        LineMarkerMode markerMode,
        int visualLineIndex,
        IReadOnlyList<int>? visualToLogicalLines)
    {
        if (markerMode == LineMarkerMode.Bullets)
            return "\u2022";

        if (markerMode == LineMarkerMode.Checklist)
        {
            var logicalLineIndex = visualToLogicalLines?[visualLineIndex] ?? visualLineIndex;
            var isContinuationRow = visualLineIndex > 0
                                    && visualToLogicalLines?[visualLineIndex - 1] == logicalLineIndex;
            if (isContinuationRow)
                return string.Empty;

            return VM?.IsChecklistLineChecked(logicalLineIndex) == true ? "\u2611" : "\u2610";
        }

        return (visualLineIndex + 1).ToString(CultureInfo.InvariantCulture);
    }
}
