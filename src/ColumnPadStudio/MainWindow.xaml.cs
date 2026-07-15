using ColumnPadStudio.Controls;
using ColumnPadStudio.Models;
using ColumnPadStudio.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio;

public partial class MainWindow : Window, INotifyPropertyChanged
{
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
        InitializeUpdateNotification();
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

}
