using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ColumnPadStudio.App.Styling;
using ColumnPadStudio.Application.Abstractions;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Infrastructure.Persistence;

namespace ColumnPadStudio.App.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private static readonly HashSet<string> CommandRelevantWorkspacePropertyNames =
    [
        nameof(WorkspaceViewModel.SelectedColumn),
        nameof(WorkspaceViewModel.HasSelectedColumn)
    ];

    private readonly IRecoveryStore _recoveryStore;
    private readonly IWorkspaceSessionStore _sessionStore;
    private readonly DispatcherTimer _recoveryTimer;
    private readonly string _defaultFontFamily;
    private WorkspaceViewModel? _selectedWorkspace;
    private string _statusText = "Ready";
    private string? _currentSessionPath;
    private bool _currentSessionSaveAsRequired = true;
    private string _lastFindText = string.Empty;
    private string _lastReplaceText = string.Empty;
    private int _lastFindColumnIndex;
    private int _lastFindCharIndex = -1;

    public ShellViewModel()
    {
        _recoveryStore = new AppDataRecoveryStore();
        _sessionStore = new JsonWorkspaceSessionStore();

        Workspaces = new ObservableCollection<WorkspaceViewModel>();
        AvailableFonts = new ObservableCollection<string>(Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .OrderBy(name => name)
            .ToList());
        _defaultFontFamily = AvailableFonts.Contains("Consolas") ? "Consolas" : AvailableFonts.FirstOrDefault() ?? "Consolas";

        AvailableLanguages = new ObservableCollection<string>(new[]
        {
            "en-US", "en-GB", "fr-FR", "de-DE", "es-ES", "it-IT", "pt-BR", "pt-PT", "nl-NL", "sv-SE", "da-DK", "nb-NO"
        });
        AvailableFontStyles = new ObservableCollection<string>(new[]
        {
            "Regular", "Bold", "Italic", "Bold Italic"
        });

        NewSessionCommand = new RelayCommand(async () => await NewSessionAsync());
        AddWorkspaceCommand = new RelayCommand(AddWorkspace);
        CloseWorkspaceCommand = new RelayCommand(async () => await CloseSelectedWorkspaceAsync(), () => Workspaces.Count > 1);
        AddColumnCommand = new RelayCommand(AddColumnToSelectedWorkspace, () => SelectedWorkspace is not null);
        RemoveColumnCommand = new RelayCommand(RemoveSelectedColumn, () => SelectedWorkspace?.HasSelectedColumn == true);
        MoveColumnLeftCommand = new RelayCommand(MoveSelectedColumnLeft, () => SelectedWorkspace?.HasSelectedColumn == true);
        MoveColumnRightCommand = new RelayCommand(MoveSelectedColumnRight, () => SelectedWorkspace?.HasSelectedColumn == true);
        OpenCommand = new RelayCommand(async () => await OpenAsync());
        SaveCommand = new RelayCommand(async () => await SaveAsync());
        SaveAsCommand = new RelayCommand(async () => await SaveAsAsync());
        ExportTextCommand = new RelayCommand(ExportText, () => SelectedWorkspace is not null);
        ExportMarkdownCommand = new RelayCommand(ExportMarkdown, () => SelectedWorkspace is not null);
        OpenWorkflowBuilderCommand = new RelayCommand(OpenWorkflowBuilder);
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        PrintCommand = new RelayCommand(PrintDocument);
        ExitCommand = new RelayCommand(ExitApplication);
        FindCommand = new RelayCommand(OpenFindDialog, () => SelectedWorkspace is not null);
        FindNextCommand = new RelayCommand(FindNext, () => SelectedWorkspace is not null);
        ReplaceAllCommand = new RelayCommand(OpenReplaceAllDialog, () => SelectedWorkspace is not null);
        SelectionToBulletsCommand = new RelayCommand(SelectionToBullets, () => CurrentColumn is not null);
        SelectionToChecklistCommand = new RelayCommand(SelectionToChecklist, () => CurrentColumn is not null);
        ToggleChecklistChecksCommand = new RelayCommand(ToggleChecklistChecks, () => CurrentColumn is not null);
        ClearSelectionCommand = new RelayCommand(ClearSelection, () => CurrentColumn is not null);
        ClearAllColumnsCommand = new RelayCommand(ClearAllColumns, () => SelectedWorkspace is not null);
        DuplicateSelectedColumnCommand = new RelayCommand(DuplicateSelectedColumn, () => CurrentColumn is not null);
        ToggleLineNumbersCommand = new RelayCommand(ToggleLineNumbers, () => SelectedWorkspace is not null);
        SingleTextModeCommand = new RelayCommand(UseSingleTextMode, () => SelectedWorkspace is not null);
        ColumnModeCommand = new RelayCommand(UseColumnMode, () => SelectedWorkspace is not null);
        SetLightModeCommand = new RelayCommand(() => SetTheme(ThemePreset.Light), () => SelectedWorkspace is not null);
        SetDarkModeCommand = new RelayCommand(() => SetTheme(ThemePreset.Dark), () => SelectedWorkspace is not null);
        SetDefaultModeCommand = new RelayCommand(() => SetTheme(ThemePreset.Default), () => SelectedWorkspace is not null);
        ResetSelectedWidthCommand = new RelayCommand(ResetSelectedWidth, () => CurrentColumn is not null);
        ResetAllWidthsCommand = new RelayCommand(ResetAllWidths, () => SelectedWorkspace is not null);
        ToggleSelectedWidthLockCommand = new RelayCommand(ToggleSelectedWidthLock, () => CurrentColumn is not null);

        AddWorkspace();

        _recoveryTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(25)
        };
        _recoveryTimer.Tick += async (_, _) => await TrySaveRecoveryAsync();
        _recoveryTimer.Start();
    }

    public ObservableCollection<WorkspaceViewModel> Workspaces { get; }
    public ObservableCollection<string> AvailableFonts { get; }
    public ObservableCollection<string> AvailableLanguages { get; }
    public ObservableCollection<string> AvailableFontStyles { get; }

    public ICommand NewSessionCommand { get; }
    public ICommand AddWorkspaceCommand { get; }
    public ICommand CloseWorkspaceCommand { get; }
    public ICommand AddColumnCommand { get; }
    public ICommand RemoveColumnCommand { get; }
    public ICommand MoveColumnLeftCommand { get; }
    public ICommand MoveColumnRightCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand ExportTextCommand { get; }
    public ICommand ExportMarkdownCommand { get; }
    public ICommand OpenWorkflowBuilderCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand FindCommand { get; }
    public ICommand FindNextCommand { get; }
    public ICommand ReplaceAllCommand { get; }
    public ICommand SelectionToBulletsCommand { get; }
    public ICommand SelectionToChecklistCommand { get; }
    public ICommand ToggleChecklistChecksCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand ClearAllColumnsCommand { get; }
    public ICommand DuplicateSelectedColumnCommand { get; }
    public ICommand ToggleLineNumbersCommand { get; }
    public ICommand SingleTextModeCommand { get; }
    public ICommand ColumnModeCommand { get; }
    public ICommand SetLightModeCommand { get; }
    public ICommand SetDarkModeCommand { get; }
    public ICommand SetDefaultModeCommand { get; }
    public ICommand ResetSelectedWidthCommand { get; }
    public ICommand ResetAllWidthsCommand { get; }
    public ICommand ToggleSelectedWidthLockCommand { get; }

    public WorkspaceViewModel? SelectedWorkspace
    {
        get => _selectedWorkspace;
        set
        {
            if (SetProperty(ref _selectedWorkspace, value))
            {
                OnPropertyChanged(nameof(CurrentColumn));
                RefreshCommandStates();
                ApplyCurrentWorkspaceTheme();
            }
        }
    }

    public ColumnViewModel? CurrentColumn => SelectedWorkspace?.SelectedColumn;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public void AddWorkspace()
    {
        var index = Workspaces.Count + 1;
        var workspace = CreateWorkspace($"Workspace {index}");
        SubscribeWorkspace(workspace);
        Workspaces.Add(workspace);
        SelectedWorkspace = workspace;
        StatusText = $"Added {workspace.Name}";
        RefreshCommandStates();
    }

    public async Task CloseSelectedWorkspaceAsync()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        if (Workspaces.Count <= 1)
        {
            StatusText = "The last remaining workspace cannot be closed.";
            return;
        }

        if (SelectedWorkspace.IsDirty)
        {
            var proceed = await ConfirmCanDiscardUnsavedChangesAsync($"closing {SelectedWorkspace.Name}");
            if (!proceed)
            {
                return;
            }
        }

        var closedName = SelectedWorkspace.Name;
        var index = Workspaces.IndexOf(SelectedWorkspace);
        UnsubscribeWorkspace(SelectedWorkspace);
        Workspaces.Remove(SelectedWorkspace);
        SelectedWorkspace = Workspaces[Math.Clamp(index - 1, 0, Workspaces.Count - 1)];
        StatusText = $"Closed {closedName}.";
        RefreshCommandStates();
        await TryClearRecoveryIfNothingDirtyAsync();
    }

    private void ApplyCurrentWorkspaceTheme()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        ThemeManager.ApplyTheme(SelectedWorkspace.ThemePreset);
    }

    private void RefreshCommandStates()
    {
        foreach (var command in new[]
        {
            NewSessionCommand,
            AddWorkspaceCommand,
            CloseWorkspaceCommand,
            AddColumnCommand,
            RemoveColumnCommand,
            MoveColumnLeftCommand,
            MoveColumnRightCommand,
            OpenCommand,
            SaveCommand,
            SaveAsCommand,
            ExportTextCommand,
            ExportMarkdownCommand,
            OpenWorkflowBuilderCommand,
            OpenSettingsCommand,
            PrintCommand,
            ExitCommand,
            FindCommand,
            FindNextCommand,
            ReplaceAllCommand,
            SelectionToBulletsCommand,
            SelectionToChecklistCommand,
            ToggleChecklistChecksCommand,
            ClearSelectionCommand,
            ClearAllColumnsCommand,
            DuplicateSelectedColumnCommand,
            ToggleLineNumbersCommand,
            SingleTextModeCommand,
            ColumnModeCommand,
            SetLightModeCommand,
            SetDarkModeCommand,
            SetDefaultModeCommand,
            ResetSelectedWidthCommand,
            ResetAllWidthsCommand,
            ToggleSelectedWidthLockCommand
        }.OfType<RelayCommand>())
        {
            command.RaiseCanExecuteChanged();
        }
    }
}
