using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ColumnPadStudio.App.ViewModels;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Logic;

namespace ColumnPadStudio.App.Views.Controls;

public partial class ColumnEditorView : UserControl
{
    private const double EditorHorizontalPadding = 12;
    private const double EditorTopPadding = 8;
    private const double EditorBottomPadding = 12;
    private const double MinimumEditorLineHeight = 14;

    private ScrollViewer? _editorScrollViewer;
    private ColumnViewModel? _columnViewModel;
    private readonly TranslateTransform _lineGuideTransform = new();

    public ColumnEditorView()
    {
        InitializeComponent();
        LineGuideSurface.RenderTransform = _lineGuideTransform;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) =>
        {
            ApplyEditorMetrics();
            UpdateLineGuideSurface();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _editorScrollViewer ??= FindDescendant<ScrollViewer>(EditorTextBox);
        if (_editorScrollViewer is not null)
        {
            _editorScrollViewer.ScrollChanged -= EditorScrollViewerOnScrollChanged;
            _editorScrollViewer.ScrollChanged += EditorScrollViewerOnScrollChanged;
        }

        DataObject.RemovePastingHandler(EditorTextBox, EditorTextBox_OnPasting);
        DataObject.AddPastingHandler(EditorTextBox, EditorTextBox_OnPasting);

        if (_columnViewModel is not null)
        {
            _columnViewModel.PropertyChanged -= ColumnViewModelOnPropertyChanged;
            _columnViewModel.PropertyChanged += ColumnViewModelOnPropertyChanged;
        }

        ApplyEditorMetrics();
        UpdateLineGuideSurface();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_editorScrollViewer is not null)
        {
            _editorScrollViewer.ScrollChanged -= EditorScrollViewerOnScrollChanged;
        }

        DataObject.RemovePastingHandler(EditorTextBox, EditorTextBox_OnPasting);

        if (_columnViewModel is not null)
        {
            _columnViewModel.PropertyChanged -= ColumnViewModelOnPropertyChanged;
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ColumnViewModel oldVm)
        {
            oldVm.PropertyChanged -= ColumnViewModelOnPropertyChanged;
        }

        _columnViewModel = e.NewValue as ColumnViewModel;
        if (_columnViewModel is not null)
        {
            _columnViewModel.PropertyChanged += ColumnViewModelOnPropertyChanged;
        }

        ApplyEditorMetrics();
        UpdateLineGuideSurface();
        ApplyRequestedSelection();
    }

    private void ColumnViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ColumnViewModel.RequestedSelectionStart) or nameof(ColumnViewModel.RequestedSelectionLength))
        {
            Dispatcher.InvokeAsync(ApplyRequestedSelection);
            return;
        }

        if (e.PropertyName is nameof(ColumnViewModel.EffectiveFontSize) or nameof(ColumnViewModel.EffectiveFontFamilyName) or nameof(ColumnViewModel.EffectiveFontStyle) or nameof(ColumnViewModel.WorkspaceLineStyle))
        {
            Dispatcher.InvokeAsync(() =>
            {
                ApplyEditorMetrics();
                UpdateLineGuideSurface();
            });
        }
    }

    private void ApplyRequestedSelection()
    {
        if (_columnViewModel is null || _columnViewModel.RequestedSelectionStart < 0)
        {
            return;
        }

        var start = Math.Min(_columnViewModel.RequestedSelectionStart, EditorTextBox.Text.Length);
        var length = Math.Min(_columnViewModel.RequestedSelectionLength, Math.Max(0, EditorTextBox.Text.Length - start));
        EditorTextBox.Focus();
        EditorTextBox.Select(start, length);
        var lineIndex = EditorTextBox.GetLineIndexFromCharacterIndex(start);
        EditorTextBox.ScrollToLine(Math.Max(0, lineIndex));
    }

    private void ContainerBorder_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ColumnViewModel vm)
        {
            vm.SelectCommand.Execute(null);
        }
    }

    private void EditorTextBox_OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ColumnViewModel vm)
        {
            vm.SelectCommand.Execute(null);
            vm.UpdateSelection(EditorTextBox.SelectionStart, EditorTextBox.SelectionLength);
        }
    }

    private void EditorTextBox_OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ColumnViewModel vm)
        {
            vm.UpdateSelection(EditorTextBox.SelectionStart, EditorTextBox.SelectionLength);
        }
    }

    private void EditorTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not ColumnViewModel vm || vm.MarkerMode != MarkerMode.Checklist)
        {
            return;
        }

        var lineIndex = EditorTextBox.GetLineIndexFromCharacterIndex(EditorTextBox.CaretIndex);
        vm.ShiftCheckedLinesAfterInsertedLine(lineIndex);
    }

    private void EditorTextBox_OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (DataContext is not ColumnViewModel vm)
        {
            return;
        }

        var rawText = e.DataObject.GetData(DataFormats.UnicodeText) as string ??
            e.DataObject.GetData(DataFormats.Text) as string;
        if (rawText is null)
        {
            return;
        }

        var transformedText = PasteTransformLogic.PrepareForEditor(rawText, vm.PastePreset);
        e.CancelCommand();
        EditorTextBox.SelectedText = transformedText;
    }

    private void GutterBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ColumnViewModel vm || vm.MarkerMode != MarkerMode.Checklist)
        {
            return;
        }

        var point = e.GetPosition(GutterBorder);
        var verticalOffset = _editorScrollViewer?.VerticalOffset ?? 0;
        var lineHeight = GetEditorMetrics().LineHeight;
        var lineIndex = Math.Max(0, (int)Math.Floor((point.Y + verticalOffset - EditorTopPadding) / lineHeight));
        vm.ToggleChecklistLine(lineIndex);
    }

    private void EditorScrollViewerOnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        GutterTransform.Y = -e.VerticalOffset;
        UpdateLineGuideOffset(e.VerticalOffset);
        if (Math.Abs(e.ViewportHeightChange) > double.Epsilon || Math.Abs(e.ExtentHeightChange) > double.Epsilon)
        {
            UpdateLineGuideSurface();
        }
    }

    private void UpdateLineGuideSurface()
    {
        if (DataContext is not ColumnViewModel || EditorTextBox.ActualWidth <= 0 || EditorTextBox.ActualHeight <= 0)
        {
            LineGuideSurface.Background = null;
            return;
        }

        var style = GetGuideStyle();
        var metrics = style.Metrics;
        var lineHeight = metrics.LineHeight;
        var width = Math.Max(0, EditorTextBox.ActualWidth - (EditorHorizontalPadding * 2));
        if (width <= 0)
        {
            LineGuideSurface.Background = null;
            return;
        }

        var pen = new Pen(style.Brush, style.Thickness);
        if (pen.CanFreeze)
        {
            pen.Freeze();
        }

        var drawing = new GeometryDrawing(
            null,
            pen,
            new LineGeometry(new Point(0, metrics.GuideY), new Point(width, metrics.GuideY)));

        if (drawing.CanFreeze)
        {
            drawing.Freeze();
        }

        var brush = new DrawingBrush(drawing)
        {
            Stretch = Stretch.None,
            TileMode = TileMode.Tile,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            Viewport = new Rect(0, 0, width, lineHeight),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, width, lineHeight),
            ViewboxUnits = BrushMappingMode.Absolute
        };

        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        LineGuideSurface.Background = brush;
        UpdateLineGuideOffset(_editorScrollViewer?.VerticalOffset ?? 0);
    }

    private void ApplyEditorMetrics()
    {
        var lineHeight = GetGuideStyle().Metrics.LineHeight;
        EditorTextBox.Padding = new Thickness(EditorHorizontalPadding, EditorTopPadding, EditorHorizontalPadding, EditorBottomPadding);

        LineGuideSurface.Margin = new Thickness(EditorHorizontalPadding, EditorTopPadding, EditorHorizontalPadding, 0);
        GutterTextBlock.LineHeight = lineHeight;
        GutterTextBlock.Padding = new Thickness(0, EditorTopPadding, 0, EditorBottomPadding);
    }

    private void UpdateLineGuideOffset(double verticalOffset)
    {
        var lineHeight = GetGuideStyle().Metrics.LineHeight;
        if (lineHeight <= 0)
        {
            _lineGuideTransform.Y = 0;
            return;
        }

        var remainder = verticalOffset % lineHeight;
        if (remainder < 0)
        {
            remainder += lineHeight;
        }

        _lineGuideTransform.Y = -remainder;
    }

    private EditorMetrics GetEditorMetrics()
    {
        var fontSize = Math.Max(1, EditorTextBox.FontSize);
        var typeface = new Typeface(EditorTextBox.FontFamily, EditorTextBox.FontStyle, EditorTextBox.FontWeight, FontStretches.Normal);
        var lineStyle = _columnViewModel?.WorkspaceLineStyle ?? EditorLineStyle.StandardRuled;
        if (typeface.TryGetGlyphTypeface(out var glyphTypeface))
        {
            var glyphBottom = glyphTypeface.Height * fontSize;
            return lineStyle switch
            {
                EditorLineStyle.LegacyRuled => BuildLegacyMetrics(fontSize, glyphBottom),
                _ => BuildStandardMetrics(fontSize, glyphBottom)
            };
        }

        var familyLineHeight = EditorTextBox.FontFamily.LineSpacing > 0
            ? EditorTextBox.FontFamily.LineSpacing * fontSize
            : fontSize * 1.25;
        return lineStyle switch
        {
            EditorLineStyle.LegacyRuled => BuildLegacyMetrics(fontSize, familyLineHeight),
            _ => BuildStandardMetrics(fontSize, familyLineHeight)
        };
    }

    private RuledGuideStyle GetGuideStyle()
    {
        var lineBrush = (Brush)System.Windows.Application.Current.Resources["EditorLineBrush"];
        var brush = lineBrush.Clone();
        var style = _columnViewModel?.WorkspaceLineStyle ?? EditorLineStyle.StandardRuled;
        var metrics = GetEditorMetrics();

        var thickness = style switch
        {
            EditorLineStyle.LegacyRuled => 0.45,
            _ => 0.5
        };

        if (style == EditorLineStyle.LegacyRuled)
        {
            brush.Opacity = 0.92;
        }

        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return new RuledGuideStyle(metrics, brush, thickness);
    }

    private static EditorMetrics BuildStandardMetrics(double fontSize, double glyphBottom)
    {
        const double lineGuideBottomInset = 0.5;
        var lineHeight = Math.Max(MinimumEditorLineHeight, Math.Max(fontSize * 1.15, glyphBottom));
        var guideY = Math.Clamp(glyphBottom - lineGuideBottomInset, 1, lineHeight - lineGuideBottomInset);
        return new EditorMetrics(lineHeight, guideY);
    }

    private static EditorMetrics BuildLegacyMetrics(double fontSize, double glyphBottom)
    {
        const double legacyGuideBottomInset = 0.35;
        var lineHeight = Math.Max(MinimumEditorLineHeight, Math.Max(fontSize * 1.05, glyphBottom + 1.8));
        var guideY = Math.Clamp(glyphBottom - legacyGuideBottomInset, 1, lineHeight - legacyGuideBottomInset);
        return new EditorMetrics(lineHeight, guideY);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private readonly record struct EditorMetrics(double LineHeight, double GuideY);
    private readonly record struct RuledGuideStyle(EditorMetrics Metrics, Brush Brush, double Thickness);
}
