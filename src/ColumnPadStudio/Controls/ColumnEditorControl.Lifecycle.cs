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
        if (_observedVm is not null)
            _observedVm.PropertyChanged -= ObservedVm_PropertyChanged;

        _observedVm = e.NewValue as ColumnViewModel;
        if (_observedVm is not null)
            _observedVm.PropertyChanged += ObservedVm_PropertyChanged;

        _lastRenderedLineNumberCount = -1;
        QueueLineNumberRefresh();
        SyncLineNumberScrollWithEditor();
    }

    private void ObservedVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ColumnViewModel.LineMarkerMode)
            or nameof(ColumnViewModel.ChecklistDone)
            or nameof(ColumnViewModel.ShowLineNumbers)
            or nameof(ColumnViewModel.WordWrap)
            or nameof(ColumnViewModel.EditorFontFamily)
            or nameof(ColumnViewModel.EditorFontSize)
            or nameof(ColumnViewModel.EditorFontStyle)
            or nameof(ColumnViewModel.EditorFontWeight))
        {
            _lastRenderedLineNumberCount = -1;
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
        AttachEditorScrollViewer();
        QueueLineNumberRefresh();
        SyncLineNumberScrollWithEditor();
    }

    private void ColumnEditorControl_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachEditorScrollViewer();

        if (_observedVm is not null)
            _observedVm.PropertyChanged -= ObservedVm_PropertyChanged;
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        QueueLineNumberRefresh();
        SyncLineNumberScrollWithEditor();
    }

    private void Editor_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ClampImagesToSurface();
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
    }

    private void DetachEditorScrollViewer()
    {
        if (_editorScrollViewer is null)
            return;

        _editorScrollViewer.ScrollChanged -= EditorScrollViewer_ScrollChanged;
        _editorScrollViewer = null;
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
        LineNumbersTransform.Y = -Math.Max(0, verticalOffset);
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

        var lineBreak = Environment.NewLine;
        var sb = new StringBuilder(lineCount * (lineBreak.Length + 3));
        for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            if (lineIndex > 0)
                sb.Append(lineBreak);

            sb.Append(GetLineNumberLabel(markerMode, lineIndex));
        }

        var renderedLineNumbers = sb.ToString();
        VM?.SetVisibleLineCount(lineCount);

        if (lineCount == _lastRenderedLineNumberCount &&
            string.Equals(LineNumbers.Text, renderedLineNumbers, StringComparison.Ordinal))
        {
            SyncLineNumberScrollWithEditor();
            return;
        }

        LineNumbers.Text = renderedLineNumbers;
        _lastRenderedLineNumberCount = lineCount;
        SyncLineNumberScrollWithEditor();
    }

    private string GetLineNumberLabel(LineMarkerMode markerMode, int lineIndex)
    {
        if (markerMode == LineMarkerMode.Bullets)
            return "\u2022";

        if (markerMode == LineMarkerMode.Checklist)
            return VM?.IsChecklistLineChecked(lineIndex) == true ? "\u2611" : "\u2610";

        return (lineIndex + 1).ToString(CultureInfo.InvariantCulture);
    }
}
