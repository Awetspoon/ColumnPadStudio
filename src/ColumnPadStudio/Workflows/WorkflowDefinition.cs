using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio.Workflows;

public sealed class WorkflowDefinition : NotifyBase
{
    private int _schemaVersion = 3;
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "New Workflow";
    private string _category = "Custom";
    private string _description = string.Empty;
    private WorkflowTriggerType _trigger = WorkflowTriggerType.Manual;
    private ObservableCollection<WorkflowDiagramNode> _nodes = [];
    private ObservableCollection<WorkflowDiagramLink> _links = [];

    public int SchemaVersion
    {
        get => _schemaVersion;
        set => Set(ref _schemaVersion, Math.Max(1, value));
    }

    public string Id
    {
        get => _id;
        set => Set(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value);
    }

    public string Name
    {
        get => _name;
        set => Set(ref _name, string.IsNullOrWhiteSpace(value) ? "New Workflow" : value.Trim());
    }

    public string Category
    {
        get => _category;
        set => Set(ref _category, string.IsNullOrWhiteSpace(value) ? "Custom" : value.Trim());
    }

    public string Description
    {
        get => _description;
        set => Set(ref _description, value ?? string.Empty);
    }

    public WorkflowTriggerType Trigger
    {
        get => _trigger;
        set => Set(ref _trigger, value);
    }

    public ObservableCollection<WorkflowDiagramNode> Nodes
    {
        get => _nodes;
        set => Set(ref _nodes, value ?? []);
    }

    public ObservableCollection<WorkflowDiagramLink> Links
    {
        get => _links;
        set => Set(ref _links, value ?? []);
    }

    [JsonIgnore]
    public string? FilePath { get; set; }
}
