using ColumnPadStudio.Domain.Text;

namespace ColumnPadStudio.ViewModels;

public sealed class WorkspaceSession : NotifyBase
{
    private string _name;
    private bool _isRenaming;
    private int _lastMultiColumnCount = 3;
    private string _cleanMetadataSignature;
    private bool _forceSessionDirty;

    public WorkspaceSession(string name, MainViewModel vm)
    {
        _name = DisplayTextRules.CleanSingleLineLabel(name, "Workspace");
        Vm = vm;
        _cleanMetadataSignature = CaptureMetadataSignature();
    }

    public string Name
    {
        get => _name;
        set
        {
            var normalized = DisplayTextRules.CleanSingleLineLabel(value, "Workspace");
            if (string.Equals(_name, normalized, StringComparison.Ordinal))
                return;

            Set(ref _name, normalized);
            NotifyDirtyStateChanged();
        }
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        set => Set(ref _isRenaming, value);
    }

    public int LastMultiColumnCount
    {
        get => _lastMultiColumnCount;
        set
        {
            var normalized = Math.Max(2, value);
            if (_lastMultiColumnCount == normalized)
                return;

            Set(ref _lastMultiColumnCount, normalized);
            NotifyDirtyStateChanged();
        }
    }

    public MainViewModel Vm { get; }
    public bool HasSessionChanges => _forceSessionDirty ||
                                     !string.Equals(_cleanMetadataSignature, CaptureMetadataSignature(), StringComparison.Ordinal);
    public bool IsDirty => Vm.IsDirty || HasSessionChanges;

    public void MarkSessionClean()
    {
        _cleanMetadataSignature = CaptureMetadataSignature();
        _forceSessionDirty = false;
        NotifyDirtyStateChanged();
    }

    public void ForceSessionDirty()
    {
        _forceSessionDirty = true;
        NotifyDirtyStateChanged();
    }

    private string CaptureMetadataSignature() => $"{Name}\0{LastMultiColumnCount}";

    private void NotifyDirtyStateChanged()
    {
        OnPropertyChanged(nameof(HasSessionChanges));
        OnPropertyChanged(nameof(IsDirty));
    }
}
