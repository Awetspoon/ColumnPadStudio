using ColumnPadStudio.Controls;
using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio;

internal sealed class WorkspaceColumnEditorCache
{
    private readonly Dictionary<WorkspaceSession, Dictionary<string, Entry>> _entriesByWorkspace = [];

    public ColumnEditorControl GetOrCreate(
        WorkspaceSession workspace,
        string columnId,
        ColumnViewModel column,
        Func<ColumnEditorControl> createEditor,
        out ColumnEditorControl? replacedEditor)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(columnId);
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(createEditor);

        if (!_entriesByWorkspace.TryGetValue(workspace, out var entries))
        {
            entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
            _entriesByWorkspace.Add(workspace, entries);
        }

        if (entries.TryGetValue(columnId, out var cachedEntry))
        {
            if (ReferenceEquals(cachedEntry.Column, column))
            {
                replacedEditor = null;
                return cachedEntry.Editor;
            }

            replacedEditor = cachedEntry.Editor;
        }
        else
        {
            replacedEditor = null;
        }

        var editor = createEditor();
        entries[columnId] = new Entry(column, editor);
        return editor;
    }

    public IReadOnlyList<ColumnEditorControl> RemoveColumnsExcept(
        WorkspaceSession workspace,
        IReadOnlyDictionary<string, ColumnViewModel> currentColumns)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(currentColumns);

        if (!_entriesByWorkspace.TryGetValue(workspace, out var entries))
            return [];

        var removedEditors = new List<ColumnEditorControl>();
        foreach (var (columnId, entry) in entries.ToArray())
        {
            if (currentColumns.TryGetValue(columnId, out var column)
                && ReferenceEquals(column, entry.Column))
            {
                continue;
            }

            entries.Remove(columnId);
            removedEditors.Add(entry.Editor);
        }

        if (entries.Count == 0)
            _entriesByWorkspace.Remove(workspace);

        return removedEditors;
    }

    public IReadOnlyList<ColumnEditorControl> RemoveWorkspacesExcept(
        IReadOnlySet<WorkspaceSession> currentWorkspaces)
    {
        ArgumentNullException.ThrowIfNull(currentWorkspaces);

        var removedEditors = new List<ColumnEditorControl>();
        foreach (var workspace in _entriesByWorkspace.Keys.ToArray())
        {
            if (currentWorkspaces.Contains(workspace))
                continue;

            removedEditors.AddRange(_entriesByWorkspace[workspace].Values.Select(entry => entry.Editor));
            _entriesByWorkspace.Remove(workspace);
        }

        return removedEditors;
    }

    public IReadOnlyList<ColumnEditorControl> Clear()
    {
        var removedEditors = _entriesByWorkspace.Values
            .SelectMany(entries => entries.Values)
            .Select(entry => entry.Editor)
            .ToArray();

        _entriesByWorkspace.Clear();
        return removedEditors;
    }

    private sealed record Entry(ColumnViewModel Column, ColumnEditorControl Editor);
}
