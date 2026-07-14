using ColumnPadStudio.Controls;
using ColumnPadStudio.Models;
using ColumnPadStudio.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ColumnPadStudio.ViewModels;

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
    private AppPreferences _appPreferences;
    private bool _autoRecoveryWarningShown;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WorkspaceSession> Workspaces { get; } = new();

    public WorkspaceSession? ActiveWorkspace
    {
        get => _activeWorkspace;
        set
        {
            if (ReferenceEquals(_activeWorkspace, value))
                return;

            var previousWorkspace = _activeWorkspace;
            var previousVm = _activeWorkspace?.Vm;
            if (previousVm is not null)
            {
                previousVm.RequestRebuildColumns -= Vm_RequestRebuildColumns;
                previousVm.PropertyChanged -= Vm_PropertyChanged;
            }
            if (previousWorkspace is not null)
                previousWorkspace.PropertyChanged -= Workspace_PropertyChanged;

            _activeWorkspace = value;
            RaisePropertyChanged(nameof(ActiveWorkspace));
            RaisePropertyChanged(nameof(ActiveVm));

            var vm = _activeWorkspace?.Vm;
            if (vm is null)
            {
                UpdateWindowTitle();
                return;
            }

            _activeWorkspace!.PropertyChanged += Workspace_PropertyChanged;
            vm.RequestRebuildColumns += Vm_RequestRebuildColumns;
            vm.PropertyChanged += Vm_PropertyChanged;
            ApplyTheme(vm.ThemePreset);
            ResetFindCursor();
            RebuildColumns();
            vm.RefreshStatus();
            UpdateWindowTitle();
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
        _appPreferences = AppPreferencesService.Load();
        ApplyTheme(_appPreferences.ThemePreset);
        WorkspaceRenameMenuItem.Click += WorkspaceRename_Click;
        WorkspaceAddMenuItem.Click += WorkspaceAdd_Click;
        WorkspaceTabs.PreviewMouseRightButtonDown += WorkspaceTabs_PreviewMouseRightButtonDown;
        Workspaces.CollectionChanged += Workspaces_CollectionChanged;

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
        var resolvedVm = vm ?? new MainViewModel();
        ApplyAppThemePreference(resolvedVm);
        var session = new WorkspaceSession(name, resolvedVm);
        Workspaces.Add(session);
        return session;
    }

    private string NextWorkspaceName()
    {
        var existingNames = Workspaces
            .Select(workspace => workspace.Name)
            .ToList();

        return WorkspaceLifecycleService.NextWorkspaceName(existingNames);
    }

    private void ApplyAppThemePreference(MainViewModel vm)
    {
        if (!string.Equals(vm.ThemePreset, _appPreferences.ThemePreset, StringComparison.Ordinal))
            vm.ThemePreset = _appPreferences.ThemePreset;
    }

    private void SyncThemePreference(string preset, MainViewModel? sourceVm)
    {
        var normalized = string.IsNullOrWhiteSpace(preset) ? "Default Mode" : preset;
        ApplyTheme(normalized);

        if (!string.Equals(_appPreferences.ThemePreset, normalized, StringComparison.Ordinal))
        {
            _appPreferences = _appPreferences with { ThemePreset = normalized };
            try
            {
                AppPreferencesService.Save(_appPreferences);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Keep the current session theme even if preferences cannot be persisted right now.
            }
        }

        foreach (var workspace in Workspaces)
        {
            if (ReferenceEquals(workspace.Vm, sourceVm))
                continue;

            if (!string.Equals(workspace.Vm.ThemePreset, normalized, StringComparison.Ordinal))
                workspace.Vm.ThemePreset = normalized;
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
            SyncThemePreference(ActiveVm.ThemePreset, sender as MainViewModel);

        if (e.PropertyName is nameof(MainViewModel.CurrentFilePath)
            or nameof(MainViewModel.CurrentFileKind)
            or nameof(MainViewModel.CurrentFileDisplayName))
        {
            UpdateWindowTitle();
        }

        if (e.PropertyName == nameof(MainViewModel.ActiveColumnId))
        {
            SyncActiveColumnVisualState(ActiveVm);
            ClearSelectionsExcept(ActiveVm.ActiveColumnId);
        }
    }

    private void Workspace_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, ActiveWorkspace) && e.PropertyName == nameof(WorkspaceSession.Name))
            UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        var documentName = ActiveWorkspace?.Vm.CurrentFileDisplayName;
        Title = string.IsNullOrWhiteSpace(documentName)
            ? "ColumnPad"
            : $"{documentName} - ColumnPad";
    }

    private void SyncActiveColumnVisualState(MainViewModel vm)
    {
        var selectedId = vm.GetActive()?.Id;
        foreach (var column in vm.Columns)
            column.IsActive = column.Id == selectedId;
    }

    private static void RunColumnAction(MainViewModel vm, ColumnViewModel column, Action action)
    {
        vm.ActiveColumnId = column.Id;
        action();
    }

    private void ClearSelectionsExcept(string? activeColumnId)
    {
        foreach (var (columnId, editor) in _editorsById)
        {
            if (!string.IsNullOrWhiteSpace(activeColumnId) && string.Equals(columnId, activeColumnId, StringComparison.Ordinal))
                continue;

            editor.ClearSelection(focusEditor: false);
        }
    }

    private void WireEditorEvents(ColumnEditorControl editor, MainViewModel vm, ColumnViewModel column)
    {
        editor.EditorFocused += (_, __) =>
        {
            var selectionChanged = !string.Equals(vm.ActiveColumnId, column.Id, StringComparison.Ordinal);
            vm.ActiveColumnId = column.Id;
            ClearSelectionsExcept(column.Id);
            if (selectionChanged)
                vm.RefreshStatus();
        };

        editor.LockWidthRequested += (_, __) => RunColumnAction(vm, column, () => ToggleColumnWidthLock(editor, vm, column));
        editor.MoveLeftRequested += (_, __) => RunColumnAction(vm, column, () => MoveActiveLeft_Click(this, new RoutedEventArgs()));
        editor.MoveRightRequested += (_, __) => RunColumnAction(vm, column, () => MoveActiveRight_Click(this, new RoutedEventArgs()));
        editor.DeleteRequested += (_, __) => RunColumnAction(vm, column, RemoveActiveWithConfirmation);
        editor.ResetWidthRequested += (_, __) => RunColumnAction(vm, column, vm.ResetActiveColumnWidth);
        editor.ResizeRequested += (_, __) => RunColumnAction(vm, column, ResizeActiveColumn);
        editor.RightEdgeResizeDeltaRequested += (_, args) => ResizeColumnFromRightEdge(editor, vm, column, args.HorizontalChange);
        editor.InsertImageRequested += (_, __) => RunColumnAction(vm, column, InsertImageIntoActiveColumn);
        editor.ImageFileDropped += (_, args) => RunColumnAction(vm, column, () => ImportImageIntoActiveColumn(args.FilePath, args.Left, args.Top));
        editor.RemoveImageRequested += (_, args) => RunColumnAction(vm, column, () => RemoveImageFromActiveColumn(args.Image));
        editor.SetFontFamilyRequested += (_, __) => RunColumnAction(vm, column, SetActiveColumnFontFamily);
        editor.IncreaseFontRequested += (_, __) => RunColumnAction(vm, column, () => AdjustActiveColumnFontSize(+1));
        editor.DecreaseFontRequested += (_, __) => RunColumnAction(vm, column, () => AdjustActiveColumnFontSize(-1));
        editor.ToggleBoldRequested += (_, __) => RunColumnAction(vm, column, ToggleActiveColumnBold);
        editor.ToggleItalicRequested += (_, __) => RunColumnAction(vm, column, ToggleActiveColumnItalic);
        editor.ResetFontRequested += (_, __) => RunColumnAction(vm, column, ResetActiveColumnFont);
    }

    private void ResizeColumnFromRightEdge(ColumnEditorControl editor, MainViewModel vm, ColumnViewModel column, double horizontalChange)
    {
        if (column.IsWidthLocked || Math.Abs(horizontalChange) < 0.1)
            return;

        var gridCol = Grid.GetColumn(editor);
        if (gridCol < 0 || gridCol >= ColumnsHost.ColumnDefinitions.Count)
            return;

        var columnDefinition = ColumnsHost.ColumnDefinitions[gridCol];
        var currentWidth = columnDefinition.ActualWidth > 0
            ? columnDefinition.ActualWidth
            : column.WidthPx ?? DefaultColumnWidthPx;

        var nextWidth = Math.Clamp(currentWidth + horizontalChange, 220.0, 5000.0);
        columnDefinition.Width = new GridLength(nextWidth, GridUnitType.Pixel);
        column.WidthPx = (int)Math.Round(nextWidth);
        UpdateColumnsHostWidth(vm);

        if (!string.Equals(vm.ActiveColumnId, column.Id, StringComparison.Ordinal))
            vm.ActiveColumnId = column.Id;

        vm.StatusText = $"Set {column.Title} width to {column.WidthPx}px.";
    }

    private static void ToggleColumnWidthLock(ColumnEditorControl editor, MainViewModel vm, ColumnViewModel column)
    {
        if (!column.IsWidthLocked && !column.WidthPx.HasValue && editor.ActualWidth > 0)
            column.WidthPx = (int)Math.Round(editor.ActualWidth);

        vm.ToggleLockActiveWidth();
    }

    private void RebuildColumns()
    {
        var vm = ActiveVm;
        SyncActiveColumnVisualState(vm);
        UpdateColumnsHostScrollState(vm);

        if (ActiveWorkspace is { } workspace && vm.Columns.Count > 1)
            workspace.LastMultiColumnCount = vm.Columns.Count;

        ColumnsHost.ColumnDefinitions.Clear();
        ColumnsHost.Children.Clear();
        _editorsById.Clear();

        for (var i = 0; i < vm.Columns.Count; i++)
        {
            var colVm = vm.Columns[i];
            colVm.CanMoveLeft = i > 0;
            colVm.CanMoveRight = i < vm.Columns.Count - 1;
            colVm.IsStandaloneDocument = vm.Columns.Count == 1;

            ColumnsHost.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = colVm.WidthPx.HasValue && colVm.WidthPx.Value > 0
                    ? new GridLength(colVm.WidthPx.Value, GridUnitType.Pixel)
                    : new GridLength(1, GridUnitType.Star),
                MinWidth = 220
            });

            var editor = new ColumnEditorControl
            {
                DataContext = colVm,
                Margin = new Thickness(0),
            };

            WireEditorEvents(editor, vm, colVm);

            _editorsById[colVm.Id] = editor;

            Grid.SetColumn(editor, i);
            ColumnsHost.Children.Add(editor);
        }

        UpdateColumnsHostWidth(vm);
    }

    private void UpdateColumnsHostScrollState(MainViewModel vm)
    {
        ColumnsScrollViewer.HorizontalScrollBarVisibility = vm.Columns.Count == 1
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
    }

    private void ColumnsScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Workspaces.Count > 0)
            UpdateColumnsHostWidth(ActiveVm);
    }

    private void UpdateColumnsHostWidth(MainViewModel vm)
    {
        const double minimumColumnWidth = 220.0;
        var minimumContentWidth = vm.Columns.Sum(column =>
            column.WidthPx.HasValue && column.WidthPx.Value > 0
                ? column.WidthPx.Value
                : minimumColumnWidth);
        var viewportWidth = ColumnsScrollViewer.ViewportWidth > 0
            ? ColumnsScrollViewer.ViewportWidth
            : ColumnsScrollViewer.ActualWidth;

        ColumnsHost.Width = Math.Max(minimumContentWidth, viewportWidth);
    }

    private void PersistWidthsFromGrid()
    {
        var vm = ActiveVm;

        for (var columnIndex = 0; columnIndex < ColumnsHost.ColumnDefinitions.Count; columnIndex++)
        {
            if (columnIndex >= vm.Columns.Count)
                break;

            var def = ColumnsHost.ColumnDefinitions[columnIndex];
            if (!vm.Columns[columnIndex].WidthPx.HasValue)
                continue;

            var px = (int)Math.Round(def.ActualWidth);
            if (px > 0)
                vm.Columns[columnIndex].WidthPx = px;
        }
    }
}
