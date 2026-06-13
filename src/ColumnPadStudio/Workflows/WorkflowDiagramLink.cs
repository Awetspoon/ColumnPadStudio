using System.Text.Json.Serialization;
using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio.Workflows;

public sealed class WorkflowDiagramLink : NotifyBase
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _fromNodeId = string.Empty;
    private string _toNodeId = string.Empty;
    private string _label = string.Empty;
    private bool _isSelected;

    public string Id
    {
        get => _id;
        set => Set(ref _id, WorkflowIdentityRules.NormalizeId(value));
    }

    public string FromNodeId
    {
        get => _fromNodeId;
        set
        {
            Set(ref _fromNodeId, value ?? string.Empty);
            OnPropertyChanged(nameof(Summary));
        }
    }

    public string ToNodeId
    {
        get => _toNodeId;
        set
        {
            Set(ref _toNodeId, value ?? string.Empty);
            OnPropertyChanged(nameof(Summary));
        }
    }

    public string Label
    {
        get => _label;
        set
        {
            Set(ref _label, value ?? string.Empty);
            OnPropertyChanged(nameof(Summary));
        }
    }

    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    [JsonIgnore]
    public string Summary
        => string.IsNullOrWhiteSpace(Label)
            ? $"{FromNodeId} -> {ToNodeId}"
            : $"{FromNodeId} -> {ToNodeId} ({Label})";
}
