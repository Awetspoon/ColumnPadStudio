using ColumnPadStudio.Controls;
using ColumnPadStudio.Models;
using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;

namespace ColumnPadStudio.SmokeTests;

internal static class ThemeAndControlSmokeTests
{
    private static readonly string[] ComboBoxItems = ["First", "Second"];

    public static void Run(SmokeTestContext tests)
    {
        VerifyColumnWidthMenuXaml(tests);

        Exception? resourceLoadException = null;
        Thread resourceLoadThread = new(() =>
        {
            try
            {
                _ = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                var resources = new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/ColumnPadStudio;component/Resources/AppResources.xaml", UriKind.Absolute)
                };

                tests.Check(resources.MergedDictionaries.Count == 3, "App resources should stay split into theme brushes, control styles, and menu styles.");
                tests.Check(resources["ControlPopupHighlightBrush"] is not null, "Theme brush resources should load from the app resource index.");
                tests.Check(resources["EditorTextBlueBrush"] is SolidColorBrush, "Theme resources should expose the column text-colour palette.");
                tests.Check(resources["PaperPatternBrush"] is SolidColorBrush, "Theme resources should expose the shared paper pattern colour.");
                tests.Check(resources[typeof(MenuItem)] is Style, "Shared menu item style should load from the app resource index.");
                tests.Check(resources["EmbeddedMenuPanelItemStyle"] is Style, "Embedded menu panel style should load from the app resource index.");
                tests.Check(resources[typeof(Button)] is Style, "Shared button style should load from the app resource index.");
                tests.Check(resources[typeof(TextBox)] is Style, "Shared textbox style should load from the app resource index.");

                Application.Current.Resources.MergedDictionaries.Add(resources);

                var styledButton = new Button { Content = "Template check" };
                styledButton.Style = (Style)resources[typeof(Button)];
                styledButton.ApplyTemplate();
                tests.Check(styledButton.Template is not null, "Shared button style should apply without missing resource errors.");

                var styledTextBox = new TextBox { Text = "Template check" };
                styledTextBox.Style = (Style)resources[typeof(TextBox)];
                styledTextBox.ApplyTemplate();
                tests.Check(styledTextBox.Template is not null, "Shared textbox style should apply without missing resource errors.");
                tests.Check(styledTextBox.IsInactiveSelectionHighlightEnabled, "Text selection should remain visible when focus moves to a menu or another control.");

                var styledComboBox = new ComboBox { ItemsSource = ComboBoxItems, SelectedIndex = 0 };
                styledComboBox.Style = (Style)resources[typeof(ComboBox)];
                var styledTabItem = new TabItem { Header = "Focus tab" };
                var styledTabControl = new TabControl { Items = { styledTabItem } };
                var focusPanel = new StackPanel
                {
                    Children =
                    {
                        styledButton,
                        styledTextBox,
                        styledComboBox,
                        styledTabControl
                    }
                };
                var focusHost = new Window
                {
                    Width = 320,
                    Height = 220,
                    Content = focusPanel,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None
                };
                focusHost.Show();
                focusHost.Activate();
                focusHost.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                VerifyKeyboardFocusBorder(styledButton, "ButtonBorder", "Buttons");
                VerifyKeyboardFocusBorder(styledComboBox, "ComboBorder", "Drop-downs");
                VerifyKeyboardFocusBorder(styledTabItem, "TabBorder", "Tabs");
                focusHost.Close();

                Color VerifySelectionPalette(string preset)
                {
                    ThemeResourceService.ApplyTheme(resources, preset);
                    var editorBackground = ((SolidColorBrush)resources["EditorBackgroundBrush"]).Color;
                    var selection = ((SolidColorBrush)resources["EditorSelectionBrush"]).Color;
                    var selectionText = ((SolidColorBrush)resources["EditorSelectionTextBrush"]).Color;
                    var inactiveSelection = ((SolidColorBrush)resources[SystemColors.InactiveSelectionHighlightBrushKey]).Color;
                    var inactiveSelectionText = ((SolidColorBrush)resources[SystemColors.InactiveSelectionHighlightTextBrushKey]).Color;
                    var systemSelection = ((SolidColorBrush)resources[SystemColors.HighlightBrushKey]).Color;
                    var systemSelectionText = ((SolidColorBrush)resources[SystemColors.HighlightTextBrushKey]).Color;

                    tests.Check(
                        SelectionContrast(Blend(editorBackground, selection, styledTextBox.SelectionOpacity), selectionText) >= 4.5,
                        $"{preset} translucent active selection should keep selected text readable.");
                    tests.Check(SelectionContrast(inactiveSelection, inactiveSelectionText) >= 4.5, $"{preset} inactive selection should keep selected text readable.");
                    tests.Check(selection == systemSelection && selectionText == systemSelectionText, $"{preset} editor and native text selection should use one palette.");
                    tests.Check(resources["PaperPatternBrush"] is SolidColorBrush, $"{preset} should expose a paper pattern colour.");
                    return ((SolidColorBrush)resources["EditorTextBlueBrush"]).Color;
                }

                void VerifyKeyboardFocusBorder(Control control, string borderName, string description)
                {
                    control.ApplyTemplate();
                    control.Focus();
                    control.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var border = control.Template.FindName(borderName, control) as Border;
                    var expectedFocusColor = ((SolidColorBrush)resources["ControlFocusBorderBrush"]).Color;
                    tests.Check(
                        control.IsKeyboardFocused
                            && border?.BorderBrush is SolidColorBrush borderBrush
                            && borderBrush.Color == expectedFocusColor,
                        $"{description} should use the shared keyboard-focus border.");
                }

                var darkBlue = VerifySelectionPalette(ThemePresetService.DarkPreset);
                var lightBlue = VerifySelectionPalette(ThemePresetService.LightPreset);
                tests.Check(darkBlue != lightBlue, "Preset text colours should adapt between dark and light themes.");
                VerifySelectionPalette(ThemePresetService.DefaultPreset);

                var paperVm = new MainViewModel { LinedPaperEnabled = true };
                var columnEditorVm = paperVm.Columns[0];
                columnEditorVm.EditorTextColor = ColumnTextColorService.Blue;
                var columnEditor = new ColumnEditorControl { DataContext = columnEditorVm };
                var columnEditorHost = new Window
                {
                    Width = 420,
                    Height = 320,
                    Content = columnEditor,
                    DataContext = new PaperHostContext(paperVm),
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None
                };
                columnEditorHost.Show();
                columnEditor.ApplyTemplate();
                tests.Check(columnEditor.FindName("ColumnTextColorMenuItem") is MenuItem, "Column formatting should expose one text-colour submenu.");
                var columnActionsButton = columnEditor.FindName("ColumnActionsButton") as Button;
                var headerGrip = columnEditor.FindName("HeaderGrip") as Border;
                var headerRenameMenu = headerGrip?.ContextMenu;
                var headerRenameItems = headerRenameMenu?.Items.OfType<MenuItem>().ToArray() ?? [];
                var headerRenameItem = headerRenameItems.SingleOrDefault();
                var actionsContextMenu = columnActionsButton?.ContextMenu;
                var actionMenuItems = actionsContextMenu?.Items.OfType<MenuItem>().ToArray() ?? [];
                tests.Check(columnActionsButton is not null && columnActionsButton.Visibility == Visibility.Visible, "Every column header should expose a visible Actions menu button.");
                tests.Check(columnActionsButton is not null && AutomationProperties.GetName(columnActionsButton) == "Column actions", "Column Actions should have an accessible name.");
                tests.Check(
                    headerRenameItem is not null && Equals(headerRenameItem.Header, "Rename Column"),
                    "Right-clicking a column header should expose Rename Column only.");
                tests.Check(
                    actionMenuItems.Any(item => Equals(item.Header, "Rename Column"))
                        && actionMenuItems.Any(item => Equals(item.Header, "Resize This Column..."))
                        && actionMenuItems
                            .FirstOrDefault(item => Equals(item.Header, "Column Font Settings"))?
                            .Items.OfType<MenuItem>().Any(item => Equals(item.Header, "Text Colour")) == true,
                    "Column Actions should retain the complete column menu, including text colour.");
                columnEditorVm.IsRenaming = false;
                if (headerRenameItem is not null)
                    headerRenameItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                tests.Check(headerRenameItem is not null && columnEditorVm.IsRenaming, "The header right-click Rename Column action should still start inline renaming.");
                columnEditorVm.IsRenaming = false;
                var actionOpenCount = 0;
                var resetWidthRequestCount = 0;
                var resizeRequestCount = 0;
                var lockWidthRequestCount = 0;
                columnEditor.ColumnActionsOpening += (_, __) => actionOpenCount++;
                columnEditor.ResetWidthRequested += (_, __) => resetWidthRequestCount++;
                columnEditor.ResizeRequested += (_, __) => resizeRequestCount++;
                columnEditor.LockWidthRequested += (_, __) => lockWidthRequestCount++;
                columnActionsButton?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(actionOpenCount == 1, "Opening Column Actions should activate its column before showing the menu.");
                tests.Check(ReferenceEquals(actionsContextMenu?.PlacementTarget, columnActionsButton), "Column Actions should open its dedicated full menu.");
                tests.Check(headerGrip is not null && headerRenameMenu is not null, "Column headers should retain their Rename-only right-click menu.");
                var resetWidthMenuItem = actionMenuItems.SingleOrDefault(item => Equals(item.Header, "Reset This Column to Default Width"));
                var resizeColumnMenuItem = actionMenuItems.SingleOrDefault(item => Equals(item.Header, "Resize This Column..."));
                var widthLockMenuItem = actionMenuItems.SingleOrDefault(item => Equals(item.Header, columnEditorVm.WidthLockActionLabel));
                var rightEdgeResizeThumb = columnEditor.FindName("RightEdgeResizeThumb") as System.Windows.Controls.Primitives.Thumb;
                tests.Check(
                    resetWidthMenuItem?.IsEnabled == true
                        && resizeColumnMenuItem?.IsEnabled == true
                        && widthLockMenuItem?.IsEnabled == true
                        && rightEdgeResizeThumb?.IsEnabled == true,
                    "Reset, resize, freeze, and drag-resize controls should be enabled for a normal multi-column strip.");

                columnEditorVm.IsWidthManagementEnabled = false;
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(
                    resetWidthMenuItem?.IsEnabled == false
                        && resizeColumnMenuItem?.IsEnabled == false
                        && widthLockMenuItem?.IsEnabled == false
                        && rightEdgeResizeThumb?.IsEnabled == false
                        && rightEdgeResizeThumb.Visibility == Visibility.Collapsed,
                    "Fit-to-window sizing should disable the per-column reset, resize, and freeze controls.");

                columnEditorVm.IsWidthManagementEnabled = true;
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                resetWidthMenuItem?.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                resizeColumnMenuItem?.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                widthLockMenuItem?.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                tests.Check(
                    resetWidthRequestCount == 1 && resizeRequestCount == 1 && lockWidthRequestCount == 1,
                    "Enabled Column Actions should keep raising the existing reset, resize, and freeze requests.");

                columnEditorVm.IsWidthLocked = true;
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(
                    Equals(widthLockMenuItem?.Header, "Allow Resize")
                        && widthLockMenuItem?.IsEnabled == true
                        && rightEdgeResizeThumb?.IsEnabled == false,
                    "A frozen column should disable edge dragging while keeping Allow Resize available.");
                columnEditorVm.IsWidthLocked = false;
                if (actionsContextMenu is not null)
                    actionsContextMenu.IsOpen = false;
                columnEditorVm.IsStandaloneDocument = true;
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(columnActionsButton?.Visibility == Visibility.Visible, "Column Actions should remain available in Single Text Mode.");

                var editorSurface = columnEditor.FindName("EditorSurface") as Grid;
                tests.Check(
                    editorSurface?.ColumnDefinitions.Count == 2
                        && Math.Abs(editorSurface.ColumnDefinitions[0].Width.Value - MainViewModel.MinimumGutterWidthPx) < 0.001,
                    "A standalone editor should start with the smallest shared gutter width without depending on a window binding.");
                paperVm.GutterWidthPx = 64;
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(editorSurface is not null && Math.Abs(editorSurface.ColumnDefinitions[0].Width.Value - 64) < 0.001, "Changing the workspace gutter width should immediately update the editor.");
                paperVm.ShowLineNumbers = false;
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(editorSurface is not null && Math.Abs(editorSurface.ColumnDefinitions[0].Width.Value) < 0.001, "Hiding line numbers should collapse the editor gutter.");
                paperVm.ShowLineNumbers = true;
                paperVm.AddColumn();
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(
                    editorSurface is not null
                        && Math.Abs(editorSurface.ColumnDefinitions[0].Width.Value - 64) < 0.001
                        && Math.Abs(paperVm.Columns[^1].LineNumberColumnWidth.Value - 64) < 0.001,
                    "Restoring line numbers and adding a column should retain the shared gutter width.");
                var paperBackground = columnEditor.FindName("EditorPaperBackground") as PaperBackground;
                var lineNumberPaperBackground = columnEditor.FindName("LineNumberPaperBackground") as PaperBackground;
                tests.Check(
                    paperBackground?.IsPaperEnabled == true
                        && lineNumberPaperBackground?.IsPaperEnabled == true
                        && Math.Abs(paperBackground.LineHeight - columnEditorVm.EditorLineHeight) < 0.001,
                    "Lined paper should fill both surfaces and follow the column's real text line height.");
                paperVm.UsePaperStyle(PaperStyle.SoftRuled);
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(
                    paperBackground?.PaperStyle == PaperStyle.SoftRuled
                        && lineNumberPaperBackground?.PaperStyle == PaperStyle.SoftRuled,
                    "Soft ruled paper should update both the writing surface and gutter from the shared paper setting.");
                paperVm.UsePaperStyle(PaperStyle.StrongRuled);
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(
                    paperBackground?.PaperStyle == PaperStyle.StrongRuled
                        && lineNumberPaperBackground?.PaperStyle == PaperStyle.StrongRuled,
                    "Strong ruled paper should update both the writing surface and gutter.");
                paperVm.LinedPaperEnabled = false;
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(
                    paperBackground?.IsPaperEnabled == false
                        && lineNumberPaperBackground?.IsPaperEnabled == false,
                    "Switching paper off should restore the normal editor and gutter backgrounds.");
                paperVm.UsePaperStyle(PaperStyle.Ruled);
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var editorTextBox = columnEditor.FindName("Editor") as TextBox;
                var lineNumbers = columnEditor.FindName("LineNumbers") as TextBlock;
                tests.Check(editorTextBox is not null, "Column editor should expose its text surface for formatting bindings.");
                editorTextBox?.ApplyTemplate();
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var editorScrollViewer = editorTextBox is null ? null : FindDescendant<ScrollViewer>(editorTextBox);
                tests.Check(
                    editorTextBox?.VerticalScrollBarVisibility == ScrollBarVisibility.Auto,
                    "Each column editor should keep its own automatic vertical scrollbar.");
                if (editorTextBox is not null)
                {
                    editorTextBox.Text = string.Join(Environment.NewLine, Enumerable.Range(1, 100).Select(index => $"Scroll line {index}"));
                    editorTextBox.ScrollToLine(40);
                    columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    tests.Check(
                        editorScrollViewer?.ScrollableHeight > 0
                            && editorScrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible,
                        "Overflowing text should show the individual column's vertical scrollbar.");
                    tests.Check(
                        paperBackground is not null
                            && lineNumberPaperBackground is not null
                            && paperBackground.VerticalOffset > 0
                            && Math.Abs(paperBackground.VerticalOffset - lineNumberPaperBackground.VerticalOffset) < 0.001,
                        "Paper rules should move with the editor and gutter when the text is scrolled.");
                }
                tests.Check(editorTextBox?.IsInactiveSelectionHighlightEnabled == true, "Column text selection should remain visible while its context menu is open.");
                tests.Check(
                    editorTextBox is not null && Math.Abs(editorTextBox.SelectionOpacity - 0.45) < 0.001,
                    "Column selection fill should stay translucent so it cannot cover the selected text.");
                tests.Check(
                    editorTextBox?.Foreground is SolidColorBrush presetBlueBrush
                        && presetBlueBrush.Color == ((SolidColorBrush)resources["EditorTextBlueBrush"]).Color,
                    "A preset column text colour should bind to the current theme palette.");

                ThemeResourceService.ApplyTheme(Application.Current.Resources, ThemePresetService.DarkPreset);
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(
                    editorTextBox?.Foreground is SolidColorBrush darkPresetBlueBrush
                        && darkPresetBlueBrush.Color == ((SolidColorBrush)Application.Current.Resources["EditorTextBlueBrush"]).Color,
                    "Preset column text colour should update when the app switches to dark mode.");

                columnEditorVm.EditorTextColor = "#123456";
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(
                    editorTextBox?.Foreground is SolidColorBrush customTextBrush
                        && customTextBrush.Color == Color.FromRgb(0x12, 0x34, 0x56),
                    "A custom column text colour should bind to its exact RGB value.");

                columnEditorVm.EditorTextColor = ColumnTextColorService.ThemeDefault;
                columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(
                    editorTextBox?.Foreground is SolidColorBrush themeTextBrush
                        && themeTextBrush.Color == ((SolidColorBrush)Application.Current.Resources["EditorForegroundBrush"]).Color,
                    "Resetting column text colour should restore the current theme foreground.");

                if (editorTextBox is not null && lineNumbers is not null)
                {
                    editorTextBox.Text = string.Join(Environment.NewLine, Enumerable.Range(1, 10_000).Select(index => $"Pasted line {index}"));
                    columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var pastedLabels = lineNumbers.Text.Split([Environment.NewLine], StringSplitOptions.None);
                    tests.Check(editorTextBox.LineCount == 10_000, "A large pasted document should keep its expected line count.");
                    tests.Check(pastedLabels.Length == editorTextBox.LineCount && pastedLabels[0] == "1" && pastedLabels[^1] == "10000", "The gutter should remain correctly numbered after a large paste.");

                    editorTextBox.Text = string.Join(" ", Enumerable.Repeat("long wrapped text", 1_000));
                    columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var wrappedLabels = lineNumbers.Text.Split([Environment.NewLine], StringSplitOptions.None);
                    tests.Check(editorTextBox.LineCount > 1, "Long text should create multiple visible rows when wrapping is enabled.");
                    tests.Check(wrappedLabels.Length == editorTextBox.LineCount && wrappedLabels[^1] == editorTextBox.LineCount.ToString(CultureInfo.InvariantCulture), "The gutter should remain aligned with wrapped text rows.");

                    var firstChecklistLine = string.Join(" ", Enumerable.Repeat("wrapped checklist item", 30));
                    var secondChecklistLine = "Second logical checklist item";
                    var wrappedChecklistText = firstChecklistLine + Environment.NewLine + secondChecklistLine;
                    var secondLogicalLineStart = firstChecklistLine.Length + Environment.NewLine.Length;
                    editorTextBox.Text = wrappedChecklistText;
                    columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                    var secondLogicalVisualLine = editorTextBox.GetLineIndexFromCharacterIndex(secondLogicalLineStart);
                    tests.Check(
                        secondLogicalVisualLine > 1,
                        "The checklist mapping check needs the first logical line to span continuation rows.");

                    columnEditor.ShowGutterBullets();
                    columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var bulletLabels = lineNumbers.Text.Split([Environment.NewLine], StringSplitOptions.None);
                    tests.Check(
                        bulletLabels.Length == editorTextBox.LineCount && bulletLabels.All(label => label == "\u2022"),
                        "Bullet mode should continue to mark every wrapped visual row.");

                    columnEditor.ShowGutterChecklist();
                    columnEditor.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    var checklistLabels = lineNumbers.Text.Split([Environment.NewLine], StringSplitOptions.None);
                    tests.Check(
                        secondLogicalVisualLine > 1
                        && checklistLabels.Length == editorTextBox.LineCount
                        && checklistLabels[0] == "\u2610"
                        && checklistLabels.Skip(1).Take(secondLogicalVisualLine - 1).All(string.IsNullOrEmpty)
                        && checklistLabels[secondLogicalVisualLine] == "\u2610",
                        "Checklist mode should show one checkbox per logical line and blank wrapped continuation rows.");

                    if (secondLogicalVisualLine > 1)
                    {
                        var continuationVisualLine = 1;
                        var continuationCharacterIndex = editorTextBox.GetCharacterIndexFromLineIndex(continuationVisualLine);

                        editorTextBox.Select(continuationCharacterIndex, 1);
                        columnEditor.ToggleChecklistChecksInSelection();
                        tests.Check(
                            columnEditorVm.IsChecklistLineChecked(0)
                            && !columnEditorVm.IsChecklistLineChecked(1),
                            "A selection on a wrapped continuation row should toggle its logical checklist item.");
                        columnEditorVm.ToggleChecklistLineChecked(0);

                        var toggleVisualLineMethod = typeof(ColumnEditorControl).GetMethod(
                            "ToggleChecklistCheckAtVisualLine",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        tests.Check(toggleVisualLineMethod is not null, "The gutter click path should expose one visual-to-logical toggle boundary.");
                        toggleVisualLineMethod?.Invoke(columnEditor, [continuationVisualLine]);
                        tests.Check(
                            columnEditorVm.IsChecklistLineChecked(0)
                            && !columnEditorVm.IsChecklistLineChecked(1),
                            "Clicking a wrapped gutter continuation row should toggle the original logical checklist item.");
                        columnEditorVm.ToggleChecklistLineChecked(0);

                        var gutterContextLineField = typeof(ColumnEditorControl).GetField(
                            "_gutterContextLineIndex",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        var toggleContextMenuItem = columnEditor.FindName("LineMarkerToggleCheckMenuItem") as MenuItem;
                        tests.Check(
                            gutterContextLineField is not null && toggleContextMenuItem is not null,
                            "The gutter context-menu mapping check should find its visual-row state and toggle command.");
                        gutterContextLineField?.SetValue(columnEditor, continuationVisualLine);
                        toggleContextMenuItem?.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        tests.Check(
                            columnEditorVm.IsChecklistLineChecked(0)
                            && !columnEditorVm.IsChecklistLineChecked(1),
                            "The gutter context menu should map a wrapped continuation row to its logical checklist item.");
                        columnEditorVm.ToggleChecklistLineChecked(0);

                        gutterContextLineField?.SetValue(columnEditor, -1);
                        editorTextBox.Select(continuationCharacterIndex, 0);
                        toggleContextMenuItem?.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        tests.Check(
                            columnEditorVm.IsChecklistLineChecked(0)
                            && !columnEditorVm.IsChecklistLineChecked(1),
                            "The gutter menu caret fallback should resolve the logical line directly under word wrap.");
                        columnEditorVm.ToggleChecklistLineChecked(0);

                        editorTextBox.Select(
                            continuationCharacterIndex,
                            secondLogicalLineStart + 1 - continuationCharacterIndex);
                        columnEditor.ToggleChecklistChecksInSelection();
                        tests.Check(
                            columnEditorVm.IsChecklistLineChecked(0)
                            && columnEditorVm.IsChecklistLineChecked(1),
                            "A wrapped multi-line selection should toggle each logical checklist item exactly once.");
                    }
                }
                ThemeResourceService.ApplyTheme(Application.Current.Resources, ThemePresetService.DefaultPreset);
                columnEditorHost.Close();
                VerifyWorkspaceEditorCache(tests);
                VerifyColumnEditorStateReuse(tests);

                var workflowBuilderWindow = new WorkflowBuilderWindow();
                workflowBuilderWindow.ApplyTemplate();
                tests.Check(workflowBuilderWindow.ViewModel is not null, "Workflow Builder window should initialize its view model.");
                tests.Check(workflowBuilderWindow.Owner is null, "Workflow Builder should stay independent from the main window so minimizing ColumnPad does not minimize it.");
                tests.Check(workflowBuilderWindow.ShowInTaskbar, "Workflow Builder should have its own taskbar entry.");
                tests.Check(workflowBuilderWindow.WindowStartupLocation == WindowStartupLocation.CenterScreen, "Workflow Builder should open as an independent window, not as an owned child.");
                tests.Check(workflowBuilderWindow.FindName("ExportWorkflowButton") is Button, "Workflow Builder should expose one grouped export action instead of separate export buttons.");
                workflowBuilderWindow.Close();

                var nestedMenu = new MenuItem { Header = "Column colour" };
                nestedMenu.Style = (Style)resources[typeof(MenuItem)];
                nestedMenu.Items.Add(new MenuItem { Header = "Blue" });
                nestedMenu.Items.Add(new MenuItem { Header = "Green" });

                var contextMenu = new ContextMenu();
                contextMenu.Items.Add(nestedMenu);
                contextMenu.ApplyTemplate();
                nestedMenu.ApplyTemplate();
                nestedMenu.IsSubmenuOpen = true;
                contextMenu.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                tests.Check(nestedMenu.Template is not null, "Nested context menu items should apply the shared app menu template.");
                nestedMenu.IsSubmenuOpen = false;
            }
            catch (Exception ex)
            {
                resourceLoadException = ex;
            }
        });

        resourceLoadThread.SetApartmentState(ApartmentState.STA);
        resourceLoadThread.Start();
        resourceLoadThread.Join();
        tests.Check(resourceLoadException is null, $"App resource dictionaries should load without XAML errors: {resourceLoadException?.Message}");
    }

    private static void VerifyColumnWidthMenuXaml(SmokeTestContext tests)
    {
        var mainWindowXamlPath = FindRepositoryFile("src", "ColumnPadStudio", "MainWindow.xaml");

        tests.Check(mainWindowXamlPath is not null, "The smoke run should be able to locate MainWindow.xaml for menu-contract checks.");
        if (mainWindowXamlPath is null)
            return;

        var document = XDocument.Load(mainWindowXamlPath);
        var menuItems = document
            .Descendants()
            .Where(element => element.Name.LocalName == "MenuItem")
            .ToArray();
        var columnsMenu = menuItems.SingleOrDefault(item => (string?)item.Attribute("Header") == "_Columns");
        var directColumnMenuItems = columnsMenu?
            .Elements()
            .Where(element => element.Name.LocalName == "MenuItem")
            .ToArray() ?? [];
        var widthMenu = directColumnMenuItems.SingleOrDefault(item => (string?)item.Attribute("Header") == "Column _Width");
        var widthMenuItems = widthMenu?
            .Elements()
            .Where(element => element.Name.LocalName == "MenuItem")
            .ToArray() ?? [];

        var standardItem = widthMenuItems.SingleOrDefault(item => (string?)item.Attribute("Header") == "_Standard (320 px)");
        tests.Check(
            (string?)standardItem?.Attribute("IsCheckable") == "True"
                && (string?)standardItem?.Attribute("IsChecked") == "{Binding IsStandardColumnWidthSelected, Mode=OneWay}"
                && (string?)standardItem?.Attribute("Click") == "UseStandardColumnWidth_Click",
            "Column Width should expose the checked Standard 320px preference action.");

        var customItem = widthMenuItems.SingleOrDefault(item => (string?)item.Attribute("Header") == "{Binding CustomColumnWidthMenuHeader}");
        tests.Check(
            (string?)customItem?.Attribute("IsCheckable") == "True"
                && (string?)customItem?.Attribute("IsChecked") == "{Binding IsCustomColumnWidthSelected, Mode=OneWay}"
                && (string?)customItem?.Attribute("Click") == "SetDefaultColumnWidth_Click",
            "Column Width should expose the checked Custom preference action.");

        var fitItem = widthMenuItems.SingleOrDefault(item => (string?)item.Attribute("Header") == "_Fit Columns to Window");
        tests.Check(
            (string?)fitItem?.Attribute("IsCheckable") == "True"
                && (string?)fitItem?.Attribute("IsChecked") == "{Binding FitColumnsToWindow, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
            "Column Width should expose Fit Columns to Window as an independent checked mode.");

        var resetSelectedItem = directColumnMenuItems.SingleOrDefault(item => (string?)item.Attribute("Header") == "Reset Selected to _Default Width");
        var resetAllItem = directColumnMenuItems.SingleOrDefault(item => (string?)item.Attribute("Header") == "Reset _All to Default Width");
        tests.Check(
            (string?)resetSelectedItem?.Attribute("Click") == "ResetActiveWidth_Click"
                && (string?)resetSelectedItem?.Attribute("IsEnabled") == "{Binding CanManageColumnWidths}"
                && (string?)resetAllItem?.Attribute("Click") == "ResetWidths_Click"
                && (string?)resetAllItem?.Attribute("IsEnabled") == "{Binding CanManageColumnWidths}"
                && (string?)resetAllItem?.Attribute("InputGestureText") == "Ctrl+R",
            "Columns should retain both guarded reset actions under their clearer default-width names.");
        tests.Check(
            directColumnMenuItems.All(item => (string?)item.Attribute("Header") is not "Reset Column Size" and not "Reset All Column Sizes"),
            "The Columns menu should not retain the ambiguous legacy reset-size labels.");

        var editorSurfacePath = FindRepositoryFile("src", "ColumnPadStudio", "MainWindow.EditorSurface.cs");
        tests.Check(editorSurfacePath is not null, "The smoke run should locate the main width command handlers for guard checks.");
        if (editorSurfacePath is not null)
        {
            var editorSurfaceSource = File.ReadAllText(editorSurfacePath);
            tests.Check(
                HasWidthManagementGuardBeforeCall(editorSurfaceSource, "ResetWidths_Click", "ResetAllColumnsToDefault(ActiveVm)")
                    && HasWidthManagementGuardBeforeCall(editorSurfaceSource, "ResetActiveWidth_Click", "ResetSelectedColumnToDefault(ActiveVm)"),
                "Reset handlers should guard keyboard and direct calls while Fit or single-column sizing disables width management.");
        }
    }

    private static string? FindRepositoryFile(params string[] relativePathSegments)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var candidateSegments = new[] { currentDirectory.FullName }.Concat(relativePathSegments).ToArray();
            var candidate = Path.Combine(candidateSegments);
            if (File.Exists(candidate))
                return candidate;

            currentDirectory = currentDirectory.Parent;
        }

        return null;
    }

    private static bool HasWidthManagementGuardBeforeCall(string source, string methodName, string guardedCall)
    {
        var methodStart = source.IndexOf($"private void {methodName}", StringComparison.Ordinal);
        if (methodStart < 0)
            return false;

        var nextMethodStart = source.IndexOf("\n    private ", methodStart + 1, StringComparison.Ordinal);
        var methodBody = nextMethodStart < 0
            ? source[methodStart..]
            : source[methodStart..nextMethodStart];
        var guardIndex = methodBody.IndexOf("if (!CanManageColumnWidths)", StringComparison.Ordinal);
        var returnIndex = methodBody.IndexOf("return;", guardIndex + 1, StringComparison.Ordinal);
        var callIndex = methodBody.IndexOf(guardedCall, StringComparison.Ordinal);
        return guardIndex >= 0 && returnIndex > guardIndex && callIndex > returnIndex;
    }

    private static void VerifyWorkspaceEditorCache(SmokeTestContext tests)
    {
        var cache = new WorkspaceColumnEditorCache();
        var firstVm = new MainViewModel();
        var firstWorkspace = new WorkspaceSession("First", firstVm);
        var firstColumn = firstVm.Columns[0];
        var replacementColumn = firstVm.Columns[1];
        var factoryCallCount = 0;

        ColumnEditorControl CreateEditor(ColumnViewModel column)
        {
            factoryCallCount++;
            return new ColumnEditorControl { DataContext = column };
        }

        var firstEditor = cache.GetOrCreate(
            firstWorkspace,
            "stable-column",
            firstColumn,
            () => CreateEditor(firstColumn),
            out var firstReplacedEditor);
        var reusedEditor = cache.GetOrCreate(
            firstWorkspace,
            "stable-column",
            firstColumn,
            () => CreateEditor(firstColumn),
            out var reusedReplacedEditor);

        tests.Check(
            ReferenceEquals(firstEditor, reusedEditor)
                && firstReplacedEditor is null
                && reusedReplacedEditor is null
                && factoryCallCount == 1,
            "The editor cache should reuse one control for the same workspace and column instance without rewiring it.");

        firstColumn.WidthPx = 476;
        firstColumn.IsWidthLocked = true;
        firstVm.ResetActiveColumnWidth(438);
        var resetWidthReusedEditor = cache.GetOrCreate(
            firstWorkspace,
            "stable-column",
            firstColumn,
            () => CreateEditor(firstColumn),
            out var resetWidthReplacedEditor);
        tests.Check(
            ReferenceEquals(firstEditor, resetWidthReusedEditor)
                && resetWidthReplacedEditor is null
                && firstColumn.WidthPx is null
                && !firstColumn.IsWidthLocked
                && factoryCallCount == 1,
            "Resetting a column width should reuse its existing editor control and preserve its event wiring.");

        var replacementEditor = cache.GetOrCreate(
            firstWorkspace,
            "stable-column",
            replacementColumn,
            () => CreateEditor(replacementColumn),
            out var replacedEditor);
        tests.Check(
            !ReferenceEquals(firstEditor, replacementEditor)
                && ReferenceEquals(replacedEditor, firstEditor)
                && factoryCallCount == 2,
            "Replacing a column object under the same ID should discard its old editor instead of keeping stale event handlers.");

        var currentColumns = new Dictionary<string, ColumnViewModel>(StringComparer.Ordinal)
        {
            ["stable-column"] = replacementColumn
        };
        tests.Check(
            cache.RemoveColumnsExcept(firstWorkspace, currentColumns).Count == 0,
            "The editor cache should retain entries still owned by the workspace's current columns.");

        var removedEditors = cache.RemoveColumnsExcept(
            firstWorkspace,
            new Dictionary<string, ColumnViewModel>(StringComparer.Ordinal));
        tests.Check(
            removedEditors.Count == 1 && ReferenceEquals(removedEditors[0], replacementEditor),
            "Removing a column should evict and return its editor for visual-tree cleanup.");

        var recreatedEditor = cache.GetOrCreate(
            firstWorkspace,
            "stable-column",
            replacementColumn,
            () => CreateEditor(replacementColumn),
            out _);
        var secondVm = new MainViewModel();
        var secondWorkspace = new WorkspaceSession("Second", secondVm);
        var secondColumn = secondVm.Columns[0];
        var secondEditor = cache.GetOrCreate(
            secondWorkspace,
            secondColumn.Id,
            secondColumn,
            () => CreateEditor(secondColumn),
            out _);

        var removedWorkspaceEditors = cache.RemoveWorkspacesExcept(new HashSet<WorkspaceSession> { secondWorkspace });
        tests.Check(
            removedWorkspaceEditors.Count == 1
                && ReferenceEquals(removedWorkspaceEditors[0], recreatedEditor)
                && ReferenceEquals(
                    cache.GetOrCreate(
                        secondWorkspace,
                        secondColumn.Id,
                        secondColumn,
                        () => CreateEditor(secondColumn),
                        out _),
                    secondEditor),
            "Closing a workspace should evict only that workspace's cached editors.");
    }

    private static void VerifyColumnEditorStateReuse(SmokeTestContext tests)
    {
        var cache = new WorkspaceColumnEditorCache();
        var vm = new MainViewModel();
        var workspace = new WorkspaceSession("State", vm);
        var column = vm.Columns[0];
        column.WordWrap = false;

        var editor = cache.GetOrCreate(
            workspace,
            column.Id,
            column,
            () => new ColumnEditorControl { DataContext = column },
            out _);
        var hostGrid = new Grid();
        hostGrid.Children.Add(editor);
        var host = new Window
        {
            Width = 360,
            Height = 220,
            Content = hostGrid,
            DataContext = new PaperHostContext(vm),
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None
        };

        host.Show();
        host.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var textBox = editor.FindName("Editor") as TextBox;
        textBox?.ApplyTemplate();
        host.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        var scrollViewer = textBox is null ? null : FindDescendant<ScrollViewer>(textBox);
        var subscriptionField = typeof(ColumnEditorControl).GetField(
            "_isObservedVmSubscribed",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var checklistLayoutVersionField = typeof(ColumnEditorControl).GetField(
            "_checklistLayoutVersion",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        tests.Check(
            textBox is not null && scrollViewer is not null,
            "The editor reuse check should resolve the real text and scroll controls.");
        tests.Check(
            subscriptionField?.GetValue(editor) is true,
            "A loaded column editor should observe its view model exactly once.");

        if (textBox is not null && scrollViewer is not null)
        {
            var longLine = new string('x', 600);
            textBox.Text = string.Join(
                Environment.NewLine,
                Enumerable.Range(1, 120).Select(index => $"{index:D3} {longLine}"));
            textBox.Select(8, 0);
            textBox.SelectedText = "edited ";
            var hadUndoState = textBox.CanUndo;
            textBox.Select(24, 11);
            scrollViewer.ScrollToHorizontalOffset(220);
            scrollViewer.ScrollToVerticalOffset(480);
            host.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            var selectionStart = textBox.SelectionStart;
            var selectionLength = textBox.SelectionLength;
            var caretIndex = textBox.CaretIndex;
            var horizontalOffset = scrollViewer.HorizontalOffset;
            var verticalOffset = scrollViewer.VerticalOffset;
            tests.Check(
                hadUndoState && horizontalOffset > 0 && verticalOffset > 0,
                "The editor state check should establish undo history and both scroll offsets before reuse.");

            hostGrid.Children.Remove(editor);
            host.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            tests.Check(
                subscriptionField?.GetValue(editor) is false,
                "An unloaded cached editor should detach its view-model observation.");

            var reusedEditor = cache.GetOrCreate(
                workspace,
                column.Id,
                column,
                () => new ColumnEditorControl { DataContext = column },
                out _);
            hostGrid.Children.Add(reusedEditor);
            host.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            tests.Check(
                ReferenceEquals(reusedEditor, editor)
                    && textBox.CanUndo
                    && textBox.SelectionStart == selectionStart
                    && textBox.SelectionLength == selectionLength
                    && textBox.CaretIndex == caretIndex,
                "Reusing a column editor should preserve its undo stack, caret, and text selection.");
            tests.Check(
                Math.Abs(scrollViewer.HorizontalOffset - horizontalOffset) < 0.5
                    && Math.Abs(scrollViewer.VerticalOffset - verticalOffset) < 0.5,
                "Reusing a column editor should restore its horizontal and vertical scroll positions.");
            tests.Check(
                subscriptionField?.GetValue(editor) is true,
                "Reloading a cached editor should reattach its view-model observation.");

            if (checklistLayoutVersionField?.GetValue(editor) is int loadedVersion)
            {
                column.EditorFontSize += 1;
                tests.Check(
                    checklistLayoutVersionField.GetValue(editor) is int changedVersion && changedVersion > loadedVersion,
                    "A reloaded editor should respond to relevant view-model changes.");

                hostGrid.Children.Remove(editor);
                host.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var unloadedVersion = (int)checklistLayoutVersionField.GetValue(editor)!;
                column.EditorFontSize += 1;
                tests.Check(
                    checklistLayoutVersionField.GetValue(editor) is int unchangedVersion && unchangedVersion == unloadedVersion,
                    "An unloaded cached editor should not retain a duplicate view-model subscription.");

                hostGrid.Children.Add(editor);
                host.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var reloadedVersion = (int)checklistLayoutVersionField.GetValue(editor)!;
                column.EditorFontSize += 1;
                tests.Check(
                    checklistLayoutVersionField.GetValue(editor) is int finalVersion && finalVersion > reloadedVersion,
                    "A cached editor should resume view-model updates after every reload.");
            }
        }

        host.Close();
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                return match;

            var nested = FindDescendant<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static double SelectionContrast(Color background, Color foreground)
    {
        static double Luminance(Color color)
        {
            static double Linearize(byte component)
            {
                var channel = component / 255d;
                return channel <= 0.04045
                    ? channel / 12.92
                    : Math.Pow((channel + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Linearize(color.R))
                + (0.7152 * Linearize(color.G))
                + (0.0722 * Linearize(color.B));
        }

        var backgroundLuminance = Luminance(background);
        var foregroundLuminance = Luminance(foreground);
        return (Math.Max(backgroundLuminance, foregroundLuminance) + 0.05)
            / (Math.Min(backgroundLuminance, foregroundLuminance) + 0.05);
    }

    private static Color Blend(Color background, Color foreground, double foregroundOpacity)
    {
        var opacity = Math.Clamp(foregroundOpacity * foreground.A / 255d, 0, 1);
        byte BlendChannel(byte backgroundChannel, byte foregroundChannel) =>
            (byte)Math.Round(backgroundChannel + ((foregroundChannel - backgroundChannel) * opacity));

        return Color.FromRgb(
            BlendChannel(background.R, foreground.R),
            BlendChannel(background.G, foreground.G),
            BlendChannel(background.B, foreground.B));
    }
}

internal sealed record PaperHostContext(MainViewModel ActiveVm);
