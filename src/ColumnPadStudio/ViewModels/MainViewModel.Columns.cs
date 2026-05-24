using ColumnPadStudio.Domain.Workspaces;

namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    private bool CanMoveActiveColumn(int delta)
    {
        var active = GetActive();
        if (active is null)
            return false;

        var currentIndex = Columns.IndexOf(active);
        var targetIndex = currentIndex + delta;
        return currentIndex >= 0 && targetIndex >= 0 && targetIndex < Columns.Count;
    }

    private void NotifyActiveColumnActionPropertiesChanged()
    {
        OnPropertyChanged(nameof(LockActiveWidthActionLabel));
        OnPropertyChanged(nameof(LockActiveWidthActionToolTip));
        OnPropertyChanged(nameof(CanMoveActiveColumnLeft));
        OnPropertyChanged(nameof(CanMoveActiveColumnRight));
    }

    public IReadOnlyList<ColumnViewModel> GetQuickJumpColumns()
    {
        return Columns.ToList();
    }

    public void SetColumnCount(int requestedCount)
    {
        var target = WorkspaceConstraints.ClampColumnCount(requestedCount);
        PromoteRawDocumentToLayoutIfNeeded(target);
        var changed = false;

        while (Columns.Count < target)
        {
            Columns.Add(MakeColumn($"Column {Columns.Count + 1}"));
            changed = true;
        }

        while (Columns.Count > target && Columns.Count > 1)
        {
            Columns.RemoveAt(Columns.Count - 1);
            changed = true;
        }

        var activeWasInvalid = ActiveColumnId is null || !Columns.Any(c => c.Id == ActiveColumnId);
        if (activeWasInvalid)
            ActiveColumnId = Columns.First().Id;

        // Prevent feedback loops from UI text updates when the effective count did not change.
        if (!changed && !activeWasInvalid)
            return;

        OnPropertyChanged(nameof(ColumnCount));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
    }

    public void AddColumn()
    {
        PromoteRawDocumentToLayoutIfNeeded(Columns.Count + 1);
        Columns.Add(MakeColumn($"Column {Columns.Count + 1}"));
        ActiveColumnId = Columns.Last().Id;
        OnPropertyChanged(nameof(ColumnCount));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
    }

    public bool RemoveActiveColumn()
    {
        if (Columns.Count <= 1)
        {
            StatusText = "You need at least 1 column.";
            return false;
        }

        var active = GetActive();
        if (active is null)
            return false;

        var idx = Columns.IndexOf(active);
        Columns.Remove(active);

        ActiveColumnId = Columns[Math.Max(0, idx - 1)].Id;
        OnPropertyChanged(nameof(ColumnCount));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
        return true;
    }

    public void ResetActiveColumnWidth()
    {
        var active = GetActive();
        if (active is null)
            return;

        active.WidthPx = null;
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        StatusText = "Selected column width reset.";
    }

    public void ResetAllColumnWidths()
    {
        foreach (var c in Columns)
            c.WidthPx = null;

        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        StatusText = "All column widths reset.";
    }

    public void SetActiveColumnWidth(int widthPx)
    {
        var active = GetActive();
        if (active is null)
            return;

        active.WidthPx = Math.Clamp(widthPx, 120, 5000);
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        StatusText = $"Set {active.Title} width to {active.WidthPx}px.";
    }

    public void ToggleLockActiveWidth()
    {
        var active = GetActive();
        if (active is null) return;

        active.IsWidthLocked = !active.IsWidthLocked;
        NotifyActiveColumnActionPropertiesChanged();
        StatusText = active.IsWidthLocked
            ? $"Froze {active.Title} width."
            : $"{active.Title} width can resize again.";
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
    }

    private bool SwapActiveColumn(int delta)
    {
        var active = GetActive();
        if (active is null)
            return false;

        var currentIndex = Columns.IndexOf(active);
        var targetIndex = currentIndex + delta;
        if (currentIndex < 0)
            return false;

        if (targetIndex < 0)
        {
            StatusText = $"{active.Title} is already the first column.";
            return false;
        }

        if (targetIndex >= Columns.Count)
        {
            StatusText = $"{active.Title} is already the last column.";
            return false;
        }

        var other = Columns[targetIndex];
        (Columns[currentIndex], Columns[targetIndex]) = (Columns[targetIndex], Columns[currentIndex]);

        ActiveColumnId = active.Id;
        NotifyActiveColumnActionPropertiesChanged();
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
        StatusText = $"Swapped {active.Title} with {other.Title}.";
        return true;
    }

    public bool MoveActiveColumnLeft()
    {
        return SwapActiveColumn(-1);
    }

    public bool MoveActiveColumnRight()
    {
        return SwapActiveColumn(+1);
    }

    public void ClearAll()
    {
        foreach (var c in Columns) c.Text = string.Empty;
        StatusText = "Cleared.";
    }

    public void DuplicateActive()
    {
        var a = GetActive();
        if (a is null) return;

        PromoteRawDocumentToLayoutIfNeeded(Columns.Count + 1);

        var copy = MakeColumn($"{a.Title} (copy)");
        copy.Text = a.Text;
        copy.WidthPx = a.WidthPx;
        copy.IsWidthLocked = a.IsWidthLocked;
        copy.PastePreset = a.PastePreset;
        copy.EditorFontFamily = a.EditorFontFamily;
        copy.EditorFontSize = a.EditorFontSize;
        copy.EditorFontStyle = a.EditorFontStyle;
        copy.EditorFontWeight = a.EditorFontWeight;
        copy.UseDefaultFont = a.UseDefaultFont;

        Columns.Add(copy);
        ActiveColumnId = copy.Id;
        OnPropertyChanged(nameof(ColumnCount));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
    }
}
