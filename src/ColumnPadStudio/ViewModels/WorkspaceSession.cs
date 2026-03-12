namespace ColumnPadStudio.ViewModels;

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
