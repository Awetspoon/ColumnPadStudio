using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio.Workflows;

public sealed class WorkflowChecklistItem : NotifyBase
{
    private string _text = string.Empty;
    private bool _isDone;

    public string Text
    {
        get => _text;
        set => Set(ref _text, value ?? string.Empty);
    }

    public bool IsDone
    {
        get => _isDone;
        set => Set(ref _isDone, value);
    }
}
