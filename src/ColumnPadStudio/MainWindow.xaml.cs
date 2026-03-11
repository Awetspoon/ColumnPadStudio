using ColumnPadStudio.Controls;
using ColumnPadStudio.ViewModels;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ColumnPadStudio.Services;

namespace ColumnPadStudio;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const double DefaultColumnWidthPx = 320.0;
    private readonly Dictionary<string, ColumnEditorControl> _editorsById = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _autoSaveTimer = new() { Interval = TimeSpan.FromSeconds(25) };

    private WorkspaceSession? _activeWorkspace;
    private string _lastFindText = string.Empty;
    private string _lastReplaceText = string.Empty;
    private int _lastFoundColumnIndex = -1;
    private int _lastFoundCharIndex = -1;
    private WorkflowBuilderWindow? _workflowBuilderWindow;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WorkspaceSession> Workspaces { get; } = new();

    public WorkspaceSession? ActiveWorkspace
    {
        get => _activeWorkspace;
        set
        {
            if (ReferenceEquals(_activeWorkspace, value))
                return;

            var previousVm = _activeWorkspace?.Vm;
            if (previousVm is not null)
            {
                previousVm.RequestRebuildColumns -= Vm_RequestRebuildColumns;
                previousVm.PropertyChanged -= Vm_PropertyChanged;
            }

            _activeWorkspace = value;
            RaisePropertyChanged(nameof(ActiveWorkspace));
            RaisePropertyChanged(nameof(ActiveVm));

            var vm = _activeWorkspace?.Vm;
            if (vm is null)
                return;

            vm.RequestRebuildColumns += Vm_RequestRebuildColumns;
            vm.PropertyChanged += Vm_PropertyChanged;
            ApplyTheme(vm.ThemePreset);
            ResetFindCursor();
            RebuildColumns();
            vm.RefreshStatus();
        }
    }

    public MainViewModel ActiveVm
    {
        get
        {
            if (ActiveWorkspace?.Vm is { } activeVm)
                return activeVm;

            if (Workspaces.Count > 0)
                return Workspaces[0].Vm;

            throw new InvalidOperationException("No workspaces are available.");
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        WorkspaceRenameMenuItem.Click += WorkspaceRename_Click;
        WorkspaceTabs.PreviewMouseRightButtonDown += WorkspaceTabs_PreviewMouseRightButtonDown;

        if (!TryOfferAutoRecovery())
            InitializeDefaultWorkspace();

        DataContext = this;

        _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        _autoSaveTimer.Start();

        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private void InitializeDefaultWorkspace()
    {
        var first = CreateWorkspace(NextWorkspaceName());
        ActiveWorkspace = first;
        WorkspaceTabs.SelectedItem = first;
    }

    private WorkspaceSession CreateWorkspace(string name, MainViewModel? vm = null)
    {
        var session = new WorkspaceSession(name, vm ?? new MainViewModel());
        Workspaces.Add(session);
        return session;
    }

    private string NextWorkspaceName()
    {
        var index = 1;
        while (true)
        {
            var candidate = $"Workspace {index}";
            if (!Workspaces.Any(w => string.Equals(w.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
            index++;
        }
    }

    private void RaisePropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void Vm_RequestRebuildColumns(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, ActiveVm))
            RebuildColumns();
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, ActiveVm))
            return;

        if (e.PropertyName == nameof(MainViewModel.ThemePreset))
            ApplyTheme(ActiveVm.ThemePreset);

        if (e.PropertyName == nameof(MainViewModel.ActiveColumnId))
            SyncActiveColumnVisualState(ActiveVm);
    }

    private void SyncActiveColumnVisualState(MainViewModel vm)
    {
        var selectedId = vm.GetActive()?.Id;
        foreach (var column in vm.Columns)
            column.IsActive = column.Id == selectedId;
    }

    private void RebuildColumns()
    {
        var vm = ActiveVm;
        SyncActiveColumnVisualState(vm);

        if (ActiveWorkspace is { } workspace && vm.Columns.Count > 1)
            workspace.LastMultiColumnCount = vm.Columns.Count;

        ColumnsHost.ColumnDefinitions.Clear();
        ColumnsHost.Children.Clear();
        _editorsById.Clear();

        var gridCol = 0;

        for (var i = 0; i < vm.Columns.Count; i++)
        {
            var colVm = vm.Columns[i];
            colVm.CanMoveLeft = i > 0;
            colVm.CanMoveRight = i < vm.Columns.Count - 1;

            var useFillWidth = vm.Columns.Count == 1 && (!colVm.WidthPx.HasValue || colVm.WidthPx.Value <= 0);
            ColumnsHost.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = useFillWidth
                    ? new GridLength(1, GridUnitType.Star)
                    : (colVm.WidthPx.HasValue && colVm.WidthPx.Value > 0)
                        ? new GridLength(colVm.WidthPx.Value, GridUnitType.Pixel)
                        : new GridLength(DefaultColumnWidthPx, GridUnitType.Pixel),
                MinWidth = 220
            });

            var editor = new ColumnEditorControl
            {
                DataContext = colVm,
                Margin = new Thickness(0),
            };

            editor.EditorFocused += (_, __) =>
            {
                var selectionChanged = !string.Equals(vm.ActiveColumnId, colVm.Id, StringComparison.Ordinal);
                vm.ActiveColumnId = colVm.Id;
                if (selectionChanged)
                    vm.RefreshStatus();
            };



            editor.LockWidthRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                vm.ToggleLockActiveWidth();
            };

            editor.MoveLeftRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                MoveActiveLeft_Click(this, new RoutedEventArgs());
            };

            editor.MoveRightRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                MoveActiveRight_Click(this, new RoutedEventArgs());
            };

            editor.DeleteRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                RemoveActiveWithConfirmation();
            };

            editor.ResetWidthRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                vm.ResetActiveColumnWidth();
            };

            editor.ResetAllWidthsRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                vm.ResetAllColumnWidths();
            };

            editor.ResizeRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                ResizeActiveColumn();
            };

            editor.SetFontFamilyRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                SetActiveColumnFontFamily();
            };

            editor.IncreaseFontRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                AdjustActiveColumnFontSize(+1);
            };

            editor.DecreaseFontRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                AdjustActiveColumnFontSize(-1);
            };

            editor.ToggleBoldRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                ToggleActiveColumnBold();
            };

            editor.ToggleItalicRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                ToggleActiveColumnItalic();
            };

            editor.ResetFontRequested += (_, __) =>
            {
                vm.ActiveColumnId = colVm.Id;
                ResetActiveColumnFont();
            };



            _editorsById[colVm.Id] = editor;

            Grid.SetColumn(editor, gridCol);
            ColumnsHost.Children.Add(editor);
            gridCol++;

            if (i < vm.Columns.Count - 1)
            {
                var leftColumn = vm.Columns[i];
                var rightColumn = vm.Columns[i + 1];
                var locked = leftColumn.IsWidthLocked || rightColumn.IsWidthLocked;

                ColumnsHost.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(10),
                    MinWidth = 6
                });

                var splitter = new GridSplitter
                {
                    Width = 10,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = Brushes.Transparent,
                    ShowsPreview = true,
                    IsEnabled = !locked,
                    Opacity = locked ? 0.35 : 1.0,
                    Cursor = locked ? Cursors.Arrow : Cursors.SizeWE
                };

                splitter.DragDelta += (_, __) => PersistWidthsFromGrid();
                splitter.DragCompleted += (_, __) => PersistWidthsFromGrid();

                Grid.SetColumn(splitter, gridCol);
                ColumnsHost.Children.Add(splitter);
                gridCol++;
            }
        }
    }

    private void PersistWidthsFromGrid()
    {
        var vm = ActiveVm;

        if (vm.Columns.Count == 1 && !vm.Columns[0].IsWidthLocked)
        {
            vm.Columns[0].WidthPx = null;
            return;
        }

        var editorIndex = 0;

        for (var gridCol = 0; gridCol < ColumnsHost.ColumnDefinitions.Count; gridCol += 2)
        {
            if (editorIndex >= vm.Columns.Count)
                break;

            var def = ColumnsHost.ColumnDefinitions[gridCol];
            var px = (int)Math.Round(def.ActualWidth);
            if (px > 0)
                vm.Columns[editorIndex].WidthPx = px;

            editorIndex++;
        }
    }

    private bool TryOfferAutoRecovery()
    {
        try
        {
            if (!WorkspaceRecoveryStore.TryLoad(out var snapshot))
                return false;

            var localTime = snapshot.SavedUtc.ToLocalTime();
            var workspaceText = snapshot.Workspaces.Count == 1
                ? "1 workspace"
                : $"{snapshot.Workspaces.Count} workspaces";
            var msg = $"Auto-recovery data for {workspaceText} from {localTime:yyyy-MM-dd HH:mm:ss} was found.{Environment.NewLine}{Environment.NewLine}Restore it now?";
            var result = MessageBox.Show(this, msg, "ColumnPad Recovery", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes && RestoreAutoRecovery(snapshot))
                return true;

            WorkspaceRecoveryStore.Clear();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WorkspaceRecoveryStore.Clear();
        }

        return false;
    }

    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            PersistWidthsFromGrid();
            SaveAutoRecoverySnapshot();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Auto-save should never interrupt editing.
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _autoSaveTimer.Stop();

        if (TryConfirmSaveBeforeExit())
            return;

        e.Cancel = true;
        _autoSaveTimer.Start();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        try
        {
            WorkspaceRecoveryStore.Clear();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }

    private bool RestoreAutoRecovery(WorkspaceRecoverySnapshot snapshot)
    {
        Workspaces.Clear();

        foreach (var workspace in snapshot.Workspaces)
        {
            var vm = new MainViewModel();
            if (!vm.LoadRecoverySnapshot(workspace))
                continue;

            CreateWorkspace(workspace.Name, vm);
        }

        if (Workspaces.Count == 0)
            return false;

        var activeIndex = Math.Clamp(snapshot.ActiveWorkspaceIndex, 0, Workspaces.Count - 1);
        ActiveWorkspace = Workspaces[activeIndex];
        WorkspaceTabs.SelectedItem = ActiveWorkspace;
        ActiveVm.StatusText = Workspaces.Count == 1
            ? "Recovered 1 workspace."
            : $"Recovered {Workspaces.Count} workspaces.";
        return true;
    }

    private void SaveAutoRecoverySnapshot()
    {
        if (Workspaces.Count == 0)
            return;

        var recoveryWorkspaces = Workspaces
            .Select(workspace => new WorkspaceRecoveryWorkspace(
                workspace.Name,
                workspace.Vm.ToLayoutJson(),
                workspace.Vm.CurrentFilePath,
                workspace.Vm.CurrentFileKind,
                workspace.Vm.IsDirty,
                workspace.Vm.RequiresSaveAsBeforeOverwrite))
            .ToList();

        var activeIndex = ActiveWorkspace is null ? 0 : Math.Max(0, Workspaces.IndexOf(ActiveWorkspace));
        WorkspaceRecoveryStore.Save(recoveryWorkspaces, activeIndex);
    }

    private void NewLayout_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmWorkspaceDestructiveAction(ActiveWorkspace, "New Layout", "Creating a new layout"))
            return;

        ActiveVm.NewLayout();
        ResetFindCursor();
    }

    private bool ConfirmWorkspaceDestructiveAction(WorkspaceSession? workspace, string dialogTitle, string actionText)
    {
        if (workspace is null)
            return true;

        var editedColumns = workspace.Vm.Columns.Where(HasEditedColumnData).ToList();
        if (editedColumns.Count == 0)
        {
            if (!workspace.Vm.IsDirty)
                return true;

            var genericMessage = $"{actionText} will permanently discard unsaved changes in {workspace.Name}.";
            return ConfirmDestructiveAction(dialogTitle, genericMessage + "\n\nAre you sure you want to continue?");
        }

        var message = editedColumns.Count == 1
            ? $"{actionText} will permanently discard the edited contents of \"{editedColumns[0].Title}\"."
            : $"{actionText} will permanently discard edited contents from {editedColumns.Count} columns.";

        return ConfirmDestructiveAction(dialogTitle, message + "\n\nAre you sure you want to continue?");
    }

    private bool ConfirmDestructiveAction(string dialogTitle, string message)
    {
        return MessageBox.Show(
            this,
            message,
            dialogTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    private bool TryRunFileAction(string dialogTitle, string actionText, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                $"Could not {actionText}.\n\n{ex.Message}",
                dialogTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    private void OpenLayout_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Supported Files (*.columnpad.json;*.txt;*.md;*.json)|*.columnpad.json;*.txt;*.md;*.json|Layout Files (*.columnpad.json;*.json)|*.columnpad.json;*.json|Text Documents (*.txt)|*.txt|Markdown Documents (*.md)|*.md|All files (*.*)|*.*",
            FilterIndex = 1
        };

        if (dlg.ShowDialog() != true)
            return;

        if (!ConfirmWorkspaceDestructiveAction(ActiveWorkspace, "Open File", "Opening a file"))
            return;

        var extension = Path.GetExtension(dlg.FileName).ToLowerInvariant();
        var fileName = Path.GetFileName(dlg.FileName);
        if (!TryRunFileAction("Open Failed", $"open {fileName}", () =>
        {
            var content = File.ReadAllText(dlg.FileName);
            if (extension == ".txt")
            {
                ActiveVm.LoadTextDocument(content, fileName, dlg.FileName, SaveFileKind.TextDocument);
            }
            else if (extension == ".md")
            {
                ActiveVm.LoadTextDocument(content, fileName, dlg.FileName, SaveFileKind.MarkdownDocument);
            }
            else
            {
                ActiveVm.LoadFromJson(content, fileName, dlg.FileName, preserveCurrentTheme: true);
            }

            ResetFindCursor();
        }))
        {
            return;
        }
    }


    private void Save_Click(object sender, RoutedEventArgs e)
    {
        PersistWidthsFromGrid();
        if (ActiveVm.CanSaveCurrentFileDirectly)
        {
            TryRunFileAction("Save Failed", $"save {Path.GetFileName(ActiveVm.CurrentFilePath)}", () => ActiveVm.SaveCurrentFile());
            return;
        }

        SaveAs_Click(sender, e);
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        PersistWidthsFromGrid();
        var dlg = CreateSaveDialog(ActiveVm);
        if (dlg.ShowDialog() != true)
            return;

        TryRunFileAction("Save Failed", $"save {Path.GetFileName(dlg.FileName)}", () => ActiveVm.SaveToPath(dlg.FileName, ActiveVm.CurrentFileKind));
    }

    private static SaveFileDialog CreateSaveDialog(MainViewModel vm)
    {
        return vm.CurrentFileKind switch
        {
            SaveFileKind.TextDocument => new SaveFileDialog
            {
                FileName = BuildSuggestedSaveFileName(vm, "document.txt"),
                Filter = "Text (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt",
                AddExtension = true
            },
            SaveFileKind.MarkdownDocument => new SaveFileDialog
            {
                FileName = BuildSuggestedSaveFileName(vm, "document.md"),
                Filter = "Markdown (*.md)|*.md|All files (*.*)|*.*",
                DefaultExt = ".md",
                AddExtension = true
            },
            SaveFileKind.TextExport => new SaveFileDialog
            {
                FileName = BuildSuggestedSaveFileName(vm, "ColumnPad_export.txt"),
                Filter = "Text (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt",
                AddExtension = true
            },
            SaveFileKind.MarkdownExport => new SaveFileDialog
            {
                FileName = BuildSuggestedSaveFileName(vm, "ColumnPad_export.md"),
                Filter = "Markdown (*.md)|*.md|All files (*.*)|*.*",
                DefaultExt = ".md",
                AddExtension = true
            },
            _ => new SaveFileDialog
            {
                FileName = BuildSuggestedSaveFileName(vm, "layout.columnpad.json"),
                Filter = "ColumnPad Layout (*.columnpad.json)|*.columnpad.json|JSON (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".columnpad.json",
                AddExtension = true
            }
        };
    }

    private static string BuildSuggestedSaveFileName(MainViewModel vm, string fallbackName)
    {
        var currentFileName = string.IsNullOrWhiteSpace(vm.CurrentFilePath)
            ? null
            : Path.GetFileName(vm.CurrentFilePath);

        if (string.IsNullOrWhiteSpace(currentFileName))
            return fallbackName;

        if (!vm.RequiresSaveAsBeforeOverwrite)
            return currentFileName;

        return AppendCopySuffix(currentFileName);
    }

    private static string AppendCopySuffix(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var baseName = string.IsNullOrWhiteSpace(extension)
            ? fileName
            : Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrWhiteSpace(baseName))
            return fileName;

        return string.IsNullOrWhiteSpace(extension)
            ? $"{baseName}-copy"
            : $"{baseName}-copy{extension}";
    }

    private bool TryConfirmSaveBeforeExit()
    {
        PersistWidthsFromGrid();

        var dirtyWorkspaces = Workspaces.Where(workspace => workspace.Vm.IsDirty).ToList();
        if (dirtyWorkspaces.Count == 0)
            return true;

        var message = BuildSaveBeforeExitMessage(dirtyWorkspaces);
        var result = MessageBox.Show(
            this,
            message,
            "Save Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes);

        if (result == MessageBoxResult.Cancel)
            return false;

        if (result == MessageBoxResult.No)
            return true;

        foreach (var workspace in dirtyWorkspaces)
        {
            if (!TrySaveWorkspaceBeforeExit(workspace))
                return false;
        }

        return true;
    }

    private static string BuildSaveBeforeExitMessage(IReadOnlyList<WorkspaceSession> dirtyWorkspaces)
    {
        if (dirtyWorkspaces.Count == 1)
            return $"Save changes to {dirtyWorkspaces[0].Name} before closing?";

        var names = dirtyWorkspaces.Take(3).Select(workspace => $"- {workspace.Name}");
        var remainder = dirtyWorkspaces.Count > 3
            ? $"\n- and {dirtyWorkspaces.Count - 3} more"
            : string.Empty;

        return $"Save changes to {dirtyWorkspaces.Count} workspaces before closing?\n\n{string.Join("\n", names)}{remainder}";
    }

    private bool TrySaveWorkspaceBeforeExit(WorkspaceSession workspace)
    {
        try
        {
            if (workspace.Vm.SaveCurrentFile())
                return true;

            var dlg = CreateSaveDialog(workspace.Vm);
            if (dlg.ShowDialog() != true)
                return false;

            workspace.Vm.SaveToPath(dlg.FileName, workspace.Vm.CurrentFileKind);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                $"Could not save {workspace.Name}.\n\n{ex.Message}",
                "Save Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }
    private void ExportTxt_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            FileName = "ColumnPad_export.txt",
            Filter = "Text (*.txt)|*.txt|All files (*.*)|*.*"
        };

        if (dlg.ShowDialog() != true)
            return;

        TryRunFileAction("Export Failed", $"export {Path.GetFileName(dlg.FileName)}", () =>
        {
            File.WriteAllText(dlg.FileName, ActiveVm.BuildExportText(), Encoding.UTF8);
            ActiveVm.StatusText = $"Exported: {Path.GetFileName(dlg.FileName)}";
        });
    }

    private void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            FileName = "ColumnPad_export.md",
            Filter = "Markdown (*.md)|*.md|All files (*.*)|*.*"
        };

        if (dlg.ShowDialog() != true)
            return;

        TryRunFileAction("Export Failed", $"export {Path.GetFileName(dlg.FileName)}", () =>
        {
            File.WriteAllText(dlg.FileName, ActiveVm.BuildExportMarkdown(), Encoding.UTF8);
            ActiveVm.StatusText = $"Exported: {Path.GetFileName(dlg.FileName)}";
        });
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true)
            return;

        var document = new FlowDocument
        {
            PagePadding = new Thickness(42),
            ColumnWidth = double.PositiveInfinity,
            FontFamily = new FontFamily(ActiveVm.EditorFontFamily),
            FontSize = ActiveVm.EditorFontSize
        };

        document.Blocks.Add(new Paragraph(new Run(ActiveVm.BuildExportText())));
        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = new Size(dlg.PrintableAreaWidth, dlg.PrintableAreaHeight);
        dlg.PrintDocument(paginator, "ColumnPad print");
        ActiveVm.StatusText = "Sent to printer.";
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void AddColumn_Click(object sender, RoutedEventArgs e)
    {
        ActiveVm.AddColumn();
    }

    private void RemoveActive_Click(object sender, RoutedEventArgs e)
    {
        RemoveActiveWithConfirmation();
    }

    private void MoveActiveLeft_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveVm.MoveActiveColumnLeft())
            GetActiveEditorControl()?.FocusEditor();
    }

    private void MoveActiveRight_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveVm.MoveActiveColumnRight())
            GetActiveEditorControl()?.FocusEditor();
    }

    private void ResetWidths_Click(object sender, RoutedEventArgs e)
    {
        ActiveVm.ResetAllColumnWidths();
    }

    private void ResetActiveWidth_Click(object sender, RoutedEventArgs e)
    {
        ActiveVm.ResetActiveColumnWidth();
    }



    private void LockActiveWidth_Click(object sender, RoutedEventArgs e)
    {
        ActiveVm.ToggleLockActiveWidth();
    }

    private void RemoveActiveWithConfirmation()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        if (HasEditedColumnData(active))
        {
            var message = BuildDeleteColumnMessage(active);
            if (!ConfirmDestructiveAction("Delete Column", message))
                return;
        }

        ActiveVm.RemoveActiveColumn();
    }

    private static string BuildDeleteColumnMessage(ColumnViewModel column)
    {
        var preview = BuildColumnPreview(column.Text);
        if (!string.IsNullOrWhiteSpace(preview))
        {
            return $"Delete selected column \"{column.Title}\"?\n\nStarts with: \"{preview}\"\n\nThis permanently removes everything in that column.";
        }

        return $"Delete selected column \"{column.Title}\"?\n\nThis permanently removes everything in that column.";
    }

    private static string? BuildColumnPreview(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var firstLine = text
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

        if (string.IsNullOrWhiteSpace(firstLine))
            return null;

        return firstLine.Length <= 64 ? firstLine : firstLine[..61] + "...";
    }

    private static bool HasEditedColumnData(ColumnViewModel column)
    {
        if (!string.IsNullOrWhiteSpace(column.Text))
            return true;

        if (column.WidthPx.HasValue)
            return true;

        if (column.IsWidthLocked)
            return true;

        if (column.PastePreset != PasteListPreset.None)
            return true;

        if (!column.UseDefaultFont)
            return true;

        return false;
    }

    private void ResizeActiveColumn()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        var current = active.WidthPx ?? (int)DefaultColumnWidthPx;
        var prompt = PromptDialog.Show(this, "Resize Column", "Width (px):", current.ToString(CultureInfo.InvariantCulture));
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        if (!int.TryParse(prompt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var widthPx))
        {
            ActiveVm.StatusText = "Invalid width value.";
            return;
        }

        ActiveVm.SetActiveColumnWidth(widthPx);
    }

    private void SetActiveColumnFontFamily()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        var prompt = PromptDialog.Show(this, "Column Font Family", "Font family:", active.EditorFontFamily);
        if (string.IsNullOrWhiteSpace(prompt))
            return;

        active.EditorFontFamily = prompt.Trim();
        active.UseDefaultFont = false;
        ActiveVm.RefreshStatus();
    }

    private void AdjustActiveColumnFontSize(double delta)
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        active.EditorFontSize = Math.Clamp(active.EditorFontSize + delta, 8.0, 40.0);
        active.UseDefaultFont = false;
        ActiveVm.RefreshStatus();
    }

    private void ToggleActiveColumnBold()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        active.EditorFontWeight = active.EditorFontWeight == FontWeights.Bold
            ? FontWeights.Normal
            : FontWeights.Bold;
        active.UseDefaultFont = false;
        ActiveVm.RefreshStatus();
    }

    private void ToggleActiveColumnItalic()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        active.EditorFontStyle = active.EditorFontStyle == FontStyles.Italic
            ? FontStyles.Normal
            : FontStyles.Italic;
        active.UseDefaultFont = false;
        ActiveVm.RefreshStatus();
    }

    private void ResetActiveColumnFont()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        active.EditorFontFamily = ActiveVm.EditorFontFamily;
        active.EditorFontSize = ActiveVm.EditorFontSize;
        active.EditorFontStyle = ActiveVm.DefaultEditorFontStyle;
        active.EditorFontWeight = ActiveVm.DefaultEditorFontWeight;
        active.UseDefaultFont = true;
        ActiveVm.RefreshStatus();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e) => ActiveVm.ClearAll();

    private void DuplicateActive_Click(object sender, RoutedEventArgs e)
    {
        ActiveVm.DuplicateActive();
    }

    private void SelectionBullets_Click(object sender, RoutedEventArgs e)
    {
        GetActiveEditorControl()?.ApplyBulletsToSelection();
    }

    private void SelectionChecklist_Click(object sender, RoutedEventArgs e)
    {
        GetActiveEditorControl()?.ApplyChecklistToSelection();
    }

    private void SelectionToggleChecks_Click(object sender, RoutedEventArgs e)
    {
        GetActiveEditorControl()?.ToggleChecklistChecksInSelection();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        ClearSelectionAndRefocusEditor();
    }

    private bool ClearSelectionAndRefocusEditor()
    {
        var editor = GetActiveEditorControl();
        if (editor is null)
            return false;

        var cleared = editor.ClearSelection();
        editor.FocusEditor();
        if (cleared)
            ActiveVm.StatusText = "Selection cleared.";
        return cleared;
    }

    private void ToolbarComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None)
            return;

        if (sender is not ComboBox comboBox)
            return;

        comboBox.IsDropDownOpen = false;
        e.Handled = true;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => ClearSelectionAndRefocusEditor()));
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        var value = PromptDialog.Show(this, "Find", "Find text:", _lastFindText);
        if (string.IsNullOrWhiteSpace(value))
            return;

        _lastFindText = value;
        ResetFindCursor();
        FindNextCore();
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastFindText))
        {
            Find_Click(sender, e);
            return;
        }

        FindNextCore();
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        var find = PromptDialog.Show(this, "Replace All", "Find text:", _lastFindText);
        if (string.IsNullOrWhiteSpace(find))
            return;

        var replacement = PromptDialog.Show(this, "Replace All", "Replace with:", _lastReplaceText);
        if (replacement is null)
            return;

        _lastFindText = find;
        _lastReplaceText = replacement;

        var totalReplacements = 0;
        foreach (var column in ActiveVm.Columns)
        {
            var (replacedText, count) = ReplaceAllWithCount(column.Text ?? string.Empty, find, replacement, StringComparison.CurrentCultureIgnoreCase);
            if (count <= 0)
                continue;

            column.Text = replacedText;
            totalReplacements += count;
        }

        ActiveVm.RefreshStatus();
        ActiveVm.StatusText = totalReplacements > 0
            ? $"Replaced {totalReplacements} occurrence(s)."
            : $"No match for '{find}'.";
    }

    private void ThemeClassic_Click(object sender, RoutedEventArgs e) => SetTheme("Light Mode");
    private void ThemeHighContrast_Click(object sender, RoutedEventArgs e) => SetTheme("Dark Mode");
    private void ThemeCompact_Click(object sender, RoutedEventArgs e) => SetTheme("Default Mode");

    private void SetTheme(string preset)
    {
        ActiveVm.ThemePreset = preset;
    }

    private void SingleTextMode_Click(object sender, RoutedEventArgs e)
    {
        var vm = ActiveVm;
        if (vm.Columns.Count <= 1)
        {
            vm.StatusText = "Already in single text mode.";
            return;
        }

        var selected = vm.GetActive();
        if (selected is null)
            return;

        var removedCount = vm.Columns.Count - 1;
        var removedLabel = removedCount == 1 ? "column" : "columns";
        var prompt = $"Single Text Mode keeps only \"{selected.Title}\" in this workspace and removes {removedCount} other {removedLabel}.\n\nContinue?";
        if (!ConfirmDestructiveAction("Single Text Mode", prompt))
            return;

        if (ActiveWorkspace is { } workspace)
            workspace.LastMultiColumnCount = Math.Max(2, vm.Columns.Count);

        var preservedTitle = selected.Title;
        var preservedText = selected.Text ?? string.Empty;
        var preservedPastePreset = selected.PastePreset;
        var preservedFontFamily = selected.EditorFontFamily;
        var preservedFontSize = selected.EditorFontSize;
        var preservedFontStyle = selected.EditorFontStyle;
        var preservedFontWeight = selected.EditorFontWeight;
        var preservedUseDefaultFont = selected.UseDefaultFont;

        vm.SetColumnCount(1);

        var single = vm.Columns[0];
        single.Title = string.IsNullOrWhiteSpace(preservedTitle) ? "Document" : preservedTitle;
        single.Text = preservedText;
        single.WidthPx = null;
        single.IsWidthLocked = false;
        single.PastePreset = preservedPastePreset;
        single.EditorFontFamily = preservedFontFamily;
        single.EditorFontSize = preservedFontSize;
        single.EditorFontStyle = preservedFontStyle;
        single.EditorFontWeight = preservedFontWeight;
        single.UseDefaultFont = preservedUseDefaultFont;

        vm.ActiveColumnId = single.Id;
        RebuildColumns();
        vm.RefreshStatus();
        vm.StatusText = "Single text mode enabled.";
    }

    private void ColumnMode_Click(object sender, RoutedEventArgs e)
    {
        var vm = ActiveVm;
        if (vm.Columns.Count > 1)
        {
            vm.StatusText = "Already in column mode.";
            return;
        }

        var targetColumns = Math.Max(2, ActiveWorkspace?.LastMultiColumnCount ?? 3);
        vm.SetColumnCount(targetColumns);
        RebuildColumns();
        vm.RefreshStatus();
        vm.StatusText = $"Column mode restored ({targetColumns} columns).";
    }

    private void OpenWorkflowBuilder_Click(object sender, RoutedEventArgs e)
    {
        if (_workflowBuilderWindow is not null)
        {
            _workflowBuilderWindow.Activate();
            _workflowBuilderWindow.Focus();
            return;
        }

        var window = new WorkflowBuilderWindow
        {
            Owner = this
        };

        window.Closed += (_, __) => _workflowBuilderWindow = null;
        _workflowBuilderWindow = window;
        window.Show();
    }

    private void ApplyTheme(string preset)
    {
        if (string.Equals(preset, "Dark Mode", StringComparison.Ordinal))
        {
            SetBrush("WindowBackgroundBrush", "#FF202020");
            SetBrush("MenuBackgroundBrush", "#FF2A2A2A");
            SetBrush("ToolbarBackgroundBrush", "#FF2A2F37");
            SetBrush("ControlForegroundBrush", "#FFF2F2F2");
            SetBrush("ControlBackgroundBrush", "#FF3A3A3A");
            SetBrush("ControlBorderBrush", "#FF6A6A6A");
            SetBrush("ControlHoverBackgroundBrush", "#FF454C56");
            SetBrush("ControlPressedBackgroundBrush", "#FF3A4452");
            SetBrush("ControlFocusBorderBrush", "#FF6FA1E0");
            SetBrush("ControlPopupBackgroundBrush", "#FF2F2F2F");
            SetBrush("ControlPopupForegroundBrush", "#FFF2F2F2");
            SetBrush("ControlPopupHighlightBrush", "#FF3B6EA8");
            SetBrush("ColumnHostBackgroundBrush", "#FF232323");
            SetBrush("ColumnHeaderBackgroundBrush", "#FF2D2D2D");
            SetBrush("ColumnSelectedHeaderBackgroundBrush", "#FF34404D");
            SetBrush("EditorBackgroundBrush", "#FF171717");
            SetBrush("EditorForegroundBrush", "#FFF2F2F2");
            SetBrush("EditorSelectionBrush", "#FF4A88CC");
            SetBrush("EditorSelectionTextBrush", "#FFFFFFFF");
            SetBrush("EditorInactiveSelectionBrush", "#FF385E8A");
            SetBrush("EditorInactiveSelectionTextBrush", "#FFFFFFFF");
            SetBrush("LinedPaperLineBrush", "#FF2B3440");

            SetBrush("LineNumberBackgroundBrush", "#FF222222");
            SetBrush("LineNumberForegroundBrush", "#FFB8B8B8");
            SetBrush("StatusBackgroundBrush", "#FF2A2A2A");
            SetBrush(SystemColors.HighlightBrushKey, "#FF4A88CC");
            SetBrush(SystemColors.HighlightTextBrushKey, "#FFFFFFFF");
            SetBrush(SystemColors.InactiveSelectionHighlightBrushKey, "#FF385E8A");
            SetBrush(SystemColors.InactiveSelectionHighlightTextBrushKey, "#FFFFFFFF");
            SetBrush(SystemColors.MenuBrushKey, "#FF2F2F2F");
            SetBrush(SystemColors.MenuTextBrushKey, "#FFF2F2F2");
            SetBrush(SystemColors.GrayTextBrushKey, "#FF9EA7B3");
            SetBrush(SystemColors.ControlTextBrushKey, "#FFF2F2F2");
            SetBrush(SystemColors.WindowBrushKey, "#FF2F2F2F");
            SetBrush(SystemColors.WindowTextBrushKey, "#FFF2F2F2");
            SetBrush(SystemColors.ControlBrushKey, "#FF3A3A3A");
            SetBrush(SystemColors.InfoBrushKey, "#FF2F2F2F");
            SetBrush(SystemColors.InfoTextBrushKey, "#FFF2F2F2");
            return;
        }

        if (string.Equals(preset, "Default Mode", StringComparison.Ordinal))
        {
            SetBrush("WindowBackgroundBrush", "#FFEDEAE1");
            SetBrush("MenuBackgroundBrush", "#FFF2EFE6");
            SetBrush("ToolbarBackgroundBrush", "#FFE6E0D3");
            SetBrush("ControlForegroundBrush", "#FF1C1C1C");
            SetBrush("ControlBackgroundBrush", "#FFF8F3E8");
            SetBrush("ControlBorderBrush", "#FFC8BFAE");
            SetBrush("ControlHoverBackgroundBrush", "#FFFFFAEE");
            SetBrush("ControlPressedBackgroundBrush", "#FFE9DFC9");
            SetBrush("ControlFocusBorderBrush", "#FF2F5E94");
            SetBrush("ControlPopupBackgroundBrush", "#FFF8F3E8");
            SetBrush("ControlPopupForegroundBrush", "#FF1C1C1C");
            SetBrush("ControlPopupHighlightBrush", "#FF2B579A");
            SetBrush("ColumnHostBackgroundBrush", "#FFF1EDE4");
            SetBrush("ColumnHeaderBackgroundBrush", "#FFD9D1C0");
            SetBrush("ColumnSelectedHeaderBackgroundBrush", "#FFE8DEC8");
            SetBrush("EditorBackgroundBrush", "#FFFFFCF4");
            SetBrush("EditorForegroundBrush", "#FF1C1C1C");
            SetBrush("EditorSelectionBrush", "#FFBECFE2");
            SetBrush("EditorSelectionTextBrush", "#FF1C1C1C");
            SetBrush("EditorInactiveSelectionBrush", "#FFD9E3EE");
            SetBrush("EditorInactiveSelectionTextBrush", "#FF1C1C1C");
            SetBrush("LinedPaperLineBrush", "#FFD6CBB9");

            SetBrush("LineNumberBackgroundBrush", "#FFEEE7D8");
            SetBrush("LineNumberForegroundBrush", "#FF7B7469");
            SetBrush("StatusBackgroundBrush", "#FFE8E2D5");
            SetBrush(SystemColors.HighlightBrushKey, "#FFBECFE2");
            SetBrush(SystemColors.HighlightTextBrushKey, "#FF1C1C1C");
            SetBrush(SystemColors.InactiveSelectionHighlightBrushKey, "#FFD9E3EE");
            SetBrush(SystemColors.InactiveSelectionHighlightTextBrushKey, "#FF1C1C1C");
            SetBrush(SystemColors.MenuBrushKey, "#FFF8F3E8");
            SetBrush(SystemColors.MenuTextBrushKey, "#FF1C1C1C");
            SetBrush(SystemColors.GrayTextBrushKey, "#FF7B7469");
            SetBrush(SystemColors.ControlTextBrushKey, "#FF1C1C1C");
            SetBrush(SystemColors.WindowBrushKey, "#FFF8F3E8");
            SetBrush(SystemColors.WindowTextBrushKey, "#FF1C1C1C");
            SetBrush(SystemColors.ControlBrushKey, "#FFF8F3E8");
            SetBrush(SystemColors.InfoBrushKey, "#FFF8F3E8");
            SetBrush(SystemColors.InfoTextBrushKey, "#FF1C1C1C");
            return;
        }

        SetBrush("WindowBackgroundBrush", "#FFEFEFEF");
        SetBrush("MenuBackgroundBrush", "#FFF5F5F5");
        SetBrush("ToolbarBackgroundBrush", "#FFE8EEF6");
        SetBrush("ControlForegroundBrush", "#FF111111");
        SetBrush("ControlBackgroundBrush", "#FFF4F4F4");
        SetBrush("ControlBorderBrush", "#FFB8B8B8");
        SetBrush("ControlHoverBackgroundBrush", "#FFFFFFFF");
        SetBrush("ControlPressedBackgroundBrush", "#FFDCE7F7");
        SetBrush("ControlFocusBorderBrush", "#FF2B579A");
        SetBrush("ControlPopupBackgroundBrush", "#FFF4F4F4");
        SetBrush("ControlPopupForegroundBrush", "#FF111111");
        SetBrush("ControlPopupHighlightBrush", "#FF2B579A");
        SetBrush("ColumnHostBackgroundBrush", "#FFF2F2F2");
        SetBrush("ColumnHeaderBackgroundBrush", "#FFE4E4E4");
        SetBrush("ColumnSelectedHeaderBackgroundBrush", "#FFE8EEF6");
        SetBrush("EditorBackgroundBrush", "#FFFFFFFF");
        SetBrush("EditorForegroundBrush", "#FF111111");
        SetBrush("EditorSelectionBrush", "#FFB7D0F2");
        SetBrush("EditorSelectionTextBrush", "#FF111111");
        SetBrush("EditorInactiveSelectionBrush", "#FFD5E3F4");
        SetBrush("EditorInactiveSelectionTextBrush", "#FF111111");
        SetBrush("LinedPaperLineBrush", "#FFE1E6ED");

        SetBrush("LineNumberBackgroundBrush", "#FFF7F7F7");
        SetBrush("LineNumberForegroundBrush", "#FF7A7A7A");
        SetBrush("StatusBackgroundBrush", "#FFF3F3F3");
        SetBrush(SystemColors.HighlightBrushKey, "#FFB7D0F2");
        SetBrush(SystemColors.HighlightTextBrushKey, "#FF111111");
        SetBrush(SystemColors.InactiveSelectionHighlightBrushKey, "#FFD5E3F4");
        SetBrush(SystemColors.InactiveSelectionHighlightTextBrushKey, "#FF111111");
        SetBrush(SystemColors.MenuBrushKey, "#FFF4F4F4");
        SetBrush(SystemColors.MenuTextBrushKey, "#FF111111");
        SetBrush(SystemColors.GrayTextBrushKey, "#FF7A7A7A");
        SetBrush(SystemColors.ControlTextBrushKey, "#FF111111");
        SetBrush(SystemColors.WindowBrushKey, "#FFF4F4F4");
        SetBrush(SystemColors.WindowTextBrushKey, "#FF111111");
        SetBrush(SystemColors.ControlBrushKey, "#FFF4F4F4");
        SetBrush(SystemColors.InfoBrushKey, "#FFF4F4F4");
        SetBrush(SystemColors.InfoTextBrushKey, "#FF111111");
    }

    private void SetBrush(string key, string hex)
        => SetBrush((object)key, hex);

    private void SetBrush(object key, string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        if (brush.CanFreeze)
            brush.Freeze();

        Resources[key] = brush;
    }

    private void NewWorkspaceTab_Click(object sender, RoutedEventArgs e)
    {
        var ws = CreateWorkspace(NextWorkspaceName());
        ActiveWorkspace = ws;
        WorkspaceTabs.SelectedItem = ws;
    }

    private void CloseWorkspaceTab_Click(object sender, RoutedEventArgs e)
    {
        if (Workspaces.Count <= 1)
        {
            ActiveVm.StatusText = "At least one workspace is required.";
            return;
        }

        var current = ResolveWorkspaceFromSender(sender);
        if (current is null)
            return;

        if (!ConfirmWorkspaceDestructiveAction(current, "Close Workspace", $"Closing {current.Name}"))
            return;

        var currentIndex = Workspaces.IndexOf(current);
        if (currentIndex < 0)
            return;

        var wasActive = ReferenceEquals(current, ActiveWorkspace);
        Workspaces.RemoveAt(currentIndex);

        if (!wasActive)
            return;

        var nextIndex = Math.Clamp(currentIndex, 0, Workspaces.Count - 1);
        ActiveWorkspace = Workspaces[nextIndex];
        WorkspaceTabs.SelectedItem = ActiveWorkspace;
    }

    private void WorkspaceTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkspaceTabs.SelectedItem is WorkspaceSession ws && !ReferenceEquals(ws, ActiveWorkspace))
            ActiveWorkspace = ws;
    }

    private void WorkspaceRename_Click(object sender, RoutedEventArgs e)
    {
        var ws = ResolveWorkspaceFromSender(sender);
        if (ws is null)
            return;

        ActiveWorkspace = ws;
        ws.IsRenaming = true;
    }

    private WorkspaceSession? ResolveWorkspaceFromSender(object sender)
    {
        if (sender is FrameworkElement { DataContext: WorkspaceSession ws })
            return ws;

        return ActiveWorkspace;
    }

    private void WorkspaceTabs_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        var tabItem = FindAncestor<TabItem>(source);
        if (tabItem?.DataContext is not WorkspaceSession ws)
            return;

        ActiveWorkspace = ws;
        WorkspaceTabs.SelectedItem = ws;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
                return match;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPreviewKeyDown(e);

        if (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.None)
        {
            FindNext_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift))
        {
            if (e.Key == Key.Left)
            {
                MoveActiveLeft_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Right)
            {
                MoveActiveRight_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            var quickIndex = ToQuickJumpIndex(e.Key);
            if (quickIndex >= 0)
            {
                QuickJumpToColumn(quickIndex);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Z)
            {
                ActiveVm.WordWrap = !ActiveVm.WordWrap;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.L)
            {
                ActiveVm.ShowLineNumbers = !ActiveVm.ShowLineNumbers;
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (e.Key is Key.OemPlus or Key.Add)
            {
                AddColumn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key is Key.OemMinus or Key.Subtract)
            {
                RemoveActive_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S)
            {
                SaveAs_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key is Key.D8 or Key.NumPad8)
            {
                SelectionBullets_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key is Key.D7 or Key.NumPad7)
            {
                SelectionChecklist_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key is Key.D1 or Key.NumPad1)
            {
                SingleTextMode_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            if (e.Key is Key.D2 or Key.NumPad2)
            {
                ColumnMode_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.B)
            {
                OpenWorkflowBuilder_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.L)
            {
                LockActiveWidth_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N)
            {
                NewWorkspaceTab_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.W)
            {
                CloseWorkspaceTab_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.E)
            {
                ExportMarkdown_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.X)
            {
                ClearAll_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.Enter)
            {
                SelectionToggleChecks_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N)
            {
                NewLayout_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.O)
            {
                OpenLayout_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S)
            {
                Save_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.E)
            {
                ExportTxt_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.P)
            {
                Print_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F)
            {
                Find_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.H)
            {
                ReplaceAll_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.D)
            {
                DuplicateActive_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.R)
            {
                ResetWidths_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
        }
    }

    private static int ToQuickJumpIndex(Key key)
    {
        return key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            Key.D7 or Key.NumPad7 => 6,
            Key.D8 or Key.NumPad8 => 7,
            Key.D9 or Key.NumPad9 => 8,
            _ => -1
        };
    }

    private void QuickJumpToColumn(int zeroBasedIndex)
    {
        var jumpTargets = ActiveVm.GetQuickJumpColumns();
        if (zeroBasedIndex < 0 || zeroBasedIndex >= jumpTargets.Count)
            return;

        var column = jumpTargets[zeroBasedIndex];
        ActiveVm.ActiveColumnId = column.Id;
        ActiveVm.StatusText = $"Jumped to {column.Title}.";

        if (_editorsById.TryGetValue(column.Id, out var editor))
            editor.FocusEditor();
    }

    private ColumnEditorControl? GetActiveEditorControl()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return null;

        _editorsById.TryGetValue(active.Id, out var editor);
        return editor;
    }

    private void ResetFindCursor()
    {
        _lastFoundColumnIndex = -1;
        _lastFoundCharIndex = -1;
    }

    private void FindNextCore()
    {
        if (string.IsNullOrWhiteSpace(_lastFindText))
            return;

        var vm = ActiveVm;
        if (vm.Columns.Count == 0)
            return;

        var startColumnIndex = 0;
        var startCharIndex = 0;

        if (_lastFoundColumnIndex >= 0 && _lastFoundColumnIndex < vm.Columns.Count)
        {
            startColumnIndex = _lastFoundColumnIndex;
            startCharIndex = _lastFoundCharIndex + _lastFindText.Length;
        }
        else
        {
            var active = vm.GetActive();
            startColumnIndex = active is null ? 0 : vm.Columns.IndexOf(active);
            if (startColumnIndex < 0)
                startColumnIndex = 0;

            if (_editorsById.TryGetValue(vm.Columns[startColumnIndex].Id, out var activeEditor))
                startCharIndex = activeEditor.SelectionStart + activeEditor.SelectionLength;
        }

        for (var offset = 0; offset < vm.Columns.Count; offset++)
        {
            var idx = (startColumnIndex + offset) % vm.Columns.Count;
            var text = vm.Columns[idx].Text ?? string.Empty;
            var from = offset == 0 ? Math.Clamp(startCharIndex, 0, text.Length) : 0;
            var hit = text.IndexOf(_lastFindText, from, StringComparison.CurrentCultureIgnoreCase);

            if (hit >= 0)
            {
                FocusFindHit(idx, hit);
                return;
            }
        }

        if (startCharIndex > 0)
        {
            var firstText = vm.Columns[startColumnIndex].Text ?? string.Empty;
            var wrapLimit = Math.Min(startCharIndex, firstText.Length);
            var wrapText = firstText[..wrapLimit];
            var wrapHit = wrapText.IndexOf(_lastFindText, StringComparison.CurrentCultureIgnoreCase);

            if (wrapHit >= 0)
            {
                FocusFindHit(startColumnIndex, wrapHit);
                return;
            }
        }

        vm.StatusText = $"No match for '{_lastFindText}'.";
    }

    private void FocusFindHit(int columnIndex, int hitIndex)
    {
        var vm = ActiveVm;
        if (columnIndex < 0 || columnIndex >= vm.Columns.Count)
            return;

        var column = vm.Columns[columnIndex];
        vm.ActiveColumnId = column.Id;
        vm.RefreshStatus();

        if (!_editorsById.TryGetValue(column.Id, out var editor))
        {
            RebuildColumns();
            _editorsById.TryGetValue(column.Id, out editor);
        }

        editor?.FocusAndSelectRange(hitIndex, _lastFindText.Length);
        _lastFoundColumnIndex = columnIndex;
        _lastFoundCharIndex = hitIndex;

        var lineNumber = ComputeLineNumber(column.Text, hitIndex);
        vm.StatusText = $"Found in {column.Title} (line {lineNumber}).";
    }

    private static int ComputeLineNumber(string? text, int charIndex)
    {
        if (string.IsNullOrEmpty(text) || charIndex <= 0)
            return 1;

        var limit = Math.Min(charIndex, text.Length);
        var line = 1;
        for (var i = 0; i < limit; i++)
        {
            if (text[i] == '\n')
                line++;
        }
        return line;
    }

    private static (string replaced, int count) ReplaceAllWithCount(
        string source,
        string find,
        string replacement,
        StringComparison comparison)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(find))
            return (source, 0);

        var sb = new StringBuilder(source.Length);
        var index = 0;
        var count = 0;

        while (index < source.Length)
        {
            var hit = source.IndexOf(find, index, comparison);
            if (hit < 0)
            {
                sb.Append(source, index, source.Length - index);
                break;
            }

            sb.Append(source, index, hit - index);
            sb.Append(replacement);
            index = hit + find.Length;
            count++;
        }

        return count == 0 ? (source, 0) : (sb.ToString(), count);
    }
}

public sealed class WorkspaceSession : NotifyBase
{
    private string _name;
    private bool _isRenaming;

    public WorkspaceSession(string name, MainViewModel vm)
    {
        _name = name;
        Vm = vm;
    }

    public string Name
    {
        get => _name;
        set => Set(ref _name, string.IsNullOrWhiteSpace(value) ? "Workspace" : value.Trim());
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        set => Set(ref _isRenaming, value);
    }

    public int LastMultiColumnCount { get; set; } = 3;

    public MainViewModel Vm { get; }
}










