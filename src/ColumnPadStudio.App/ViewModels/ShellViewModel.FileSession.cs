using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ColumnPadStudio.Application.Services;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Logic;
using ColumnPadStudio.Domain.Models;
using Microsoft.Win32;

namespace ColumnPadStudio.App.ViewModels;

public sealed partial class ShellViewModel
{
    public async Task<bool> OpenAsync()
    {
        if (!await ConfirmCanDiscardUnsavedChangesAsync("opening another file"))
        {
            return false;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "ColumnPad Supported Files (*.columnpad.json;*.json;*.txt;*.md)|*.columnpad.json;*.json;*.txt;*.md|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        var content = await File.ReadAllTextAsync(dialog.FileName);
        var kind = WorkspaceFileClassifier.Classify(dialog.FileName, content);

        if (kind == WorkspaceFileKind.Session)
        {
            var session = WorkspaceImportService.ImportSession(content);
            LoadSession(session, dialog.FileName, saveAsRequired: false);
            await ClearRecoveryAsync();
            StatusText = $"Opened {Path.GetFileName(dialog.FileName)}";
            return true;
        }

        var workspaceDocument = WorkspaceImportService.ImportWorkspace(dialog.FileName, content, kind);
        WorkspaceDocumentLogic.Normalize(workspaceDocument, _defaultFontFamily);
        var workspace = new WorkspaceViewModel(workspaceDocument);
        workspace.ApplyOpenedFileContext(dialog.FileName, kind, saveAsRequired: kind is WorkspaceFileKind.RawText or WorkspaceFileKind.RawMarkdown or WorkspaceFileKind.TextExport or WorkspaceFileKind.MarkdownExport);

        ClearWorkspaces();
        SubscribeWorkspace(workspace);
        Workspaces.Add(workspace);
        SelectedWorkspace = workspace;
        _currentSessionPath = null;
        _currentSessionSaveAsRequired = true;
        await ClearRecoveryAsync();
        RefreshCommandStates();
        StatusText = $"Opened {Path.GetFileName(dialog.FileName)}";
        return true;
    }

    public async Task<bool> SaveAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentSessionPath) && !_currentSessionSaveAsRequired)
        {
            return await SaveSessionToPathAsync(_currentSessionPath);
        }

        if (Workspaces.Count > 1)
        {
            return await SaveSessionAsAsync();
        }

        if (SelectedWorkspace is null)
        {
            return false;
        }

        if (SelectedWorkspace.CanDirectSave)
        {
            return await SaveWorkspaceToPathAsync(SelectedWorkspace, SelectedWorkspace.FilePath!, SelectedWorkspace.FileKind, clearSaveAs: true);
        }

        return await SaveWorkspaceAsAsync(SelectedWorkspace);
    }

    public async Task<bool> SaveAsAsync()
    {
        if (Workspaces.Count > 1 || !string.IsNullOrWhiteSpace(_currentSessionPath))
        {
            return await SaveSessionAsAsync();
        }

        if (SelectedWorkspace is null)
        {
            return false;
        }

        return await SaveWorkspaceAsAsync(SelectedWorkspace);
    }

    public void ExportText()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Text File (*.txt)|*.txt",
            DefaultExt = ".txt",
            FileName = SelectedWorkspace.Name + ".txt"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, WorkspaceExporter.ToText(SelectedWorkspace.ToDocument()));
        StatusText = $"Exported: {Path.GetFileName(dialog.FileName)}";
    }

    public void ExportMarkdown()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Markdown File (*.md)|*.md",
            DefaultExt = ".md",
            FileName = SelectedWorkspace.Name + ".md"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, WorkspaceExporter.ToMarkdown(SelectedWorkspace.ToDocument()));
        StatusText = $"Exported: {Path.GetFileName(dialog.FileName)}";
    }

    public void OpenWorkflowBuilder()
    {
        var window = new Views.WorkflowBuilderWindow
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.Show();
        StatusText = "Opened Workflow Builder";
    }

    public void OpenSettings()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        var window = new Views.SettingsWindow(this)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
        ApplyCurrentWorkspaceTheme();
        StatusText = "Updated workspace settings";
    }

    public bool RenameWorkspace(WorkspaceViewModel? workspace, string? requestedName)
    {
        if (workspace is null)
        {
            return false;
        }

        var normalizedName = string.IsNullOrWhiteSpace(requestedName)
            ? workspace.Name
            : requestedName.Trim();

        if (string.Equals(workspace.Name, normalizedName, StringComparison.Ordinal))
        {
            return false;
        }

        var previousName = workspace.Name;
        workspace.Name = normalizedName;
        StatusText = $"Renamed {previousName} to {workspace.Name}.";
        return true;
    }

    public WorkspaceSessionDocument ToSessionDocument()
    {
        return new WorkspaceSessionDocument
        {
            ActiveWorkspaceIndex = SelectedWorkspace is null ? 0 : Workspaces.IndexOf(SelectedWorkspace),
            Workspaces = Workspaces.Select(w => w.ToDocument()).ToList()
        };
    }

    public async Task TryRestoreRecoveryAsync()
    {
        var snapshot = await _recoveryStore.TryLoadSnapshotAsync();
        if (snapshot is null)
        {
            return;
        }

        if (!RecoverySessionLogic.ShouldOfferRestore(snapshot))
        {
            await _recoveryStore.ClearAsync();
            return;
        }

        var restoreResult = MessageBox.Show(
            RecoverySessionLogic.BuildRestorePrompt(snapshot),
            "ColumnPad Backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (restoreResult == MessageBoxResult.Yes)
        {
            LoadSession(
                snapshot.Session,
                snapshot.SessionPath,
                snapshot.SessionSaveAsRequired,
                markNeedsSave: true,
                workspaceStates: snapshot.WorkspaceStates);
            StatusText = "Restored unsaved backup.";
            return;
        }

        await _recoveryStore.ClearAsync();
    }

    private async Task ClearRecoveryAsync()
    {
        await _recoveryStore.ClearAsync();
    }

    public async Task NewSessionAsync()
    {
        if (!await ConfirmCanDiscardUnsavedChangesAsync("starting a new layout"))
        {
            return;
        }

        ClearWorkspaces();
        _currentSessionPath = null;
        _currentSessionSaveAsRequired = true;
        AddWorkspace();
        await ClearRecoveryAsync();
        StatusText = "Started new layout";
    }

    private void PrintDocument()
    {
        if (SelectedWorkspace is null)
        {
            return;
        }

        try
        {
            var dialog = new PrintDialog();
            if (dialog.ShowDialog() != true)
            {
                StatusText = "Print cancelled.";
                return;
            }

            var document = SelectedWorkspace.ToDocument();
            var printableBody = WorkspaceExporter.ToText(document);
            var flowDocument = new FlowDocument(new Paragraph(new Run(printableBody)))
            {
                FontFamily = new FontFamily(SelectedWorkspace.FontFamilyName),
                FontSize = SelectedWorkspace.FontSize,
                PagePadding = new Thickness(48),
                Foreground = Brushes.Black,
                Background = Brushes.White
            };

            dialog.PrintDocument(((IDocumentPaginatorSource)flowDocument).DocumentPaginator, SelectedWorkspace.Name);
            StatusText = "Sent to printer.";
        }
        catch
        {
            StatusText = "Print failed.";
        }
    }

    private void ExitApplication()
    {
        System.Windows.Application.Current.MainWindow?.Close();
    }

    public async Task<bool> ConfirmCloseApplicationAsync()
    {
        _recoveryTimer.Stop();

        if (!await ConfirmCanDiscardUnsavedChangesAsync("closing ColumnPad"))
        {
            _recoveryTimer.Start();
            return false;
        }

        await ClearRecoveryAsync();
        return true;
    }

    private bool HasDirtyWorkspaces() => Workspaces.Any(workspace => workspace.IsDirty);

    private async Task<bool> ConfirmCanDiscardUnsavedChangesAsync(string actionText)
    {
        if (!HasDirtyWorkspaces())
        {
            return true;
        }

        var result = MessageBox.Show(
            $"You have unsaved changes. Save before {actionText}?",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
        {
            StatusText = $"Cancelled {actionText}.";
            return false;
        }

        if (result == MessageBoxResult.No)
        {
            return true;
        }

        return await SaveDirtyWorkspacesAsync();
    }

    private async Task<bool> SaveDirtyWorkspacesAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentSessionPath) && !_currentSessionSaveAsRequired)
        {
            return await SaveSessionToPathAsync(_currentSessionPath);
        }

        if (Workspaces.Count > 1)
        {
            return await SaveSessionAsAsync();
        }

        if (SelectedWorkspace is null || !SelectedWorkspace.IsDirty)
        {
            return true;
        }

        if (SelectedWorkspace.CanDirectSave)
        {
            return await SaveWorkspaceToPathAsync(SelectedWorkspace, SelectedWorkspace.FilePath!, SelectedWorkspace.FileKind, clearSaveAs: true);
        }

        return await SaveWorkspaceAsAsync(SelectedWorkspace);
    }

    private WorkspaceViewModel CreateWorkspace(string name)
    {
        return new WorkspaceViewModel(WorkspaceDocumentLogic.CreateDefaultWorkspace(name, _defaultFontFamily));
    }

    private async Task<bool> SaveSessionAsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "ColumnPad Session (*.columnpad.json)|*.columnpad.json|JSON Files (*.json)|*.json",
            DefaultExt = ".columnpad.json",
            FileName = "columnpad-session.columnpad.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        return await SaveSessionToPathAsync(dialog.FileName);
    }

    private async Task<bool> SaveSessionToPathAsync(string path)
    {
        await _sessionStore.SaveAsync(path, ToSessionDocument());
        _currentSessionPath = path;
        _currentSessionSaveAsRequired = false;

        foreach (var workspace in Workspaces)
        {
            workspace.MarkSaved(path, WorkspaceFileKind.Session, saveAsRequired: false);
        }

        await TryClearRecoveryIfNothingDirtyAsync();

        StatusText = $"Saved: {Path.GetFileName(path)}";
        return true;
    }

    private async Task<bool> SaveWorkspaceAsAsync(WorkspaceViewModel workspace)
    {
        var targetKind = GetPreferredSaveKind(workspace);
        var dialog = BuildSaveDialog(workspace, targetKind);
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        var actualKind = WorkspaceFileClassifier.Classify(dialog.FileName, string.Empty);
        if (actualKind == WorkspaceFileKind.Session)
        {
            actualKind = targetKind;
        }

        return await SaveWorkspaceToPathAsync(workspace, dialog.FileName, actualKind, clearSaveAs: true);
    }

    private async Task<bool> SaveWorkspaceToPathAsync(WorkspaceViewModel workspace, string path, WorkspaceFileKind fileKind, bool clearSaveAs)
    {
        var document = workspace.ToDocument();
        var content = fileKind switch
        {
            WorkspaceFileKind.RawText => workspace.Columns.FirstOrDefault()?.Text ?? string.Empty,
            WorkspaceFileKind.RawMarkdown => workspace.Columns.FirstOrDefault()?.Text ?? string.Empty,
            WorkspaceFileKind.TextExport => WorkspaceExporter.ToText(document),
            WorkspaceFileKind.MarkdownExport => WorkspaceExporter.ToMarkdown(document),
            _ => WorkspaceImportService.SerializeLayout(document)
        };

        await File.WriteAllTextAsync(path, content);
        var resolvedFileKind = workspace.Columns.Count > 1 && (fileKind == WorkspaceFileKind.RawText || fileKind == WorkspaceFileKind.RawMarkdown)
            ? WorkspaceFileKind.Layout
            : fileKind;
        workspace.MarkSaved(path, resolvedFileKind, saveAsRequired: !clearSaveAs);

        await TryClearRecoveryIfNothingDirtyAsync();

        StatusText = $"Saved: {Path.GetFileName(path)}";
        return true;
    }

    private WorkspaceFileKind GetPreferredSaveKind(WorkspaceViewModel workspace)
    {
        if (workspace.Columns.Count > 1)
        {
            return WorkspaceFileKind.Layout;
        }

        return workspace.FileKind switch
        {
            WorkspaceFileKind.RawText => WorkspaceFileKind.RawText,
            WorkspaceFileKind.RawMarkdown => WorkspaceFileKind.RawMarkdown,
            WorkspaceFileKind.TextExport => WorkspaceFileKind.TextExport,
            WorkspaceFileKind.MarkdownExport => WorkspaceFileKind.MarkdownExport,
            _ => WorkspaceFileKind.Layout
        };
    }

    private SaveFileDialog BuildSaveDialog(WorkspaceViewModel workspace, WorkspaceFileKind fileKind)
    {
        var fileName = workspace.Name;
        return fileKind switch
        {
            WorkspaceFileKind.RawText => new SaveFileDialog { Filter = "Text File (*.txt)|*.txt", DefaultExt = ".txt", FileName = fileName + ".txt" },
            WorkspaceFileKind.RawMarkdown => new SaveFileDialog { Filter = "Markdown File (*.md)|*.md", DefaultExt = ".md", FileName = fileName + ".md" },
            WorkspaceFileKind.TextExport => new SaveFileDialog { Filter = "Text File (*.txt)|*.txt", DefaultExt = ".txt", FileName = fileName + ".txt" },
            WorkspaceFileKind.MarkdownExport => new SaveFileDialog { Filter = "Markdown File (*.md)|*.md", DefaultExt = ".md", FileName = fileName + ".md" },
            _ => new SaveFileDialog { Filter = "ColumnPad Layout (*.json)|*.json", DefaultExt = ".json", FileName = fileName + ".json" }
        };
    }

    private void LoadSession(WorkspaceSessionDocument session, string? sessionPath, bool saveAsRequired)
    {
        LoadSession(session, sessionPath, saveAsRequired, markNeedsSave: false, workspaceStates: null);
    }

    private void LoadSession(WorkspaceSessionDocument session, string? sessionPath, bool saveAsRequired, bool markNeedsSave, IReadOnlyCollection<RecoveryWorkspaceState>? workspaceStates)
    {
        ClearWorkspaces();
        var workspaceStateLookup = workspaceStates?
            .Where(state => state.WorkspaceId != Guid.Empty)
            .GroupBy(state => state.WorkspaceId)
            .ToDictionary(group => group.Key, group => group.Last());

        foreach (var workspace in session.Workspaces)
        {
            WorkspaceDocumentLogic.Normalize(workspace, _defaultFontFamily);
            var vm = new WorkspaceViewModel(workspace);
            if (workspaceStateLookup is not null && workspaceStateLookup.TryGetValue(vm.Id, out var workspaceState))
            {
                vm.ApplyRecoveryState(workspaceState);
            }
            else
            {
                vm.MarkSaved(sessionPath, WorkspaceFileKind.Session, saveAsRequired);
            }

            if (markNeedsSave)
            {
                vm.MarkNeedsSave();
            }

            SubscribeWorkspace(vm);
            Workspaces.Add(vm);
        }

        if (Workspaces.Count == 0)
        {
            AddWorkspace();
        }
        else
        {
            SelectedWorkspace = Workspaces[Math.Clamp(session.ActiveWorkspaceIndex, 0, Workspaces.Count - 1)];
        }

        _currentSessionPath = sessionPath;
        _currentSessionSaveAsRequired = saveAsRequired;
        RefreshCommandStates();
    }

    private async Task TrySaveRecoveryAsync()
    {
        try
        {
            if (!HasDirtyWorkspaces())
            {
                await _recoveryStore.ClearAsync();
                return;
            }

            foreach (var workspace in Workspaces)
            {
                workspace.PersistRuntimeStateForRecovery();
            }

            var session = ToSessionDocument();
            if (!RecoverySessionLogic.ShouldPersistSnapshot(session))
            {
                await _recoveryStore.ClearAsync();
                return;
            }

            await _recoveryStore.SaveSnapshotAsync(new RecoverySnapshot
            {
                Session = session,
                SessionPath = _currentSessionPath,
                SessionSaveAsRequired = _currentSessionSaveAsRequired,
                WorkspaceStates = Workspaces.Select(workspace => workspace.CaptureRecoveryState()).ToList()
            });
        }
        catch
        {
            // Backup writes should never interrupt editing.
        }
    }

    private async Task TryClearRecoveryIfNothingDirtyAsync()
    {
        if (!HasDirtyWorkspaces())
        {
            await ClearRecoveryAsync();
        }
    }

    private void SubscribeWorkspace(WorkspaceViewModel workspace)
    {
        workspace.PropertyChanged += WorkspaceOnPropertyChanged;
    }

    private void UnsubscribeWorkspace(WorkspaceViewModel workspace)
    {
        workspace.PropertyChanged -= WorkspaceOnPropertyChanged;
    }

    private void ClearWorkspaces()
    {
        foreach (var workspace in Workspaces)
        {
            UnsubscribeWorkspace(workspace);
        }

        Workspaces.Clear();
    }

    private void WorkspaceOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender != SelectedWorkspace)
        {
            return;
        }

        if (e.PropertyName == nameof(WorkspaceViewModel.ThemePreset))
        {
            ApplyCurrentWorkspaceTheme();
        }

        if (string.IsNullOrWhiteSpace(e.PropertyName) ||
            CommandRelevantWorkspacePropertyNames.Contains(e.PropertyName))
        {
            OnPropertyChanged(nameof(CurrentColumn));
            RefreshCommandStates();
        }
    }
}
