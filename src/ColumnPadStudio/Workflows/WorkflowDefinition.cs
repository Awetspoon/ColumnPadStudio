using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using ColumnPadStudio.Domain.Text;
using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio.Workflows;

public sealed class WorkflowDefinition : NotifyBase
{
    public const string WorkflowFileType = "ColumnPadWorkflow";
    public const int CurrentSchemaVersion = 4;

    private int _schemaVersion = CurrentSchemaVersion;
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "New Workflow";
    private string _category = "Custom";
    private string _description = string.Empty;
    private ObservableCollection<WorkflowDiagramNode> _nodes = [];
    private ObservableCollection<WorkflowDiagramLink> _links = [];

    public string FileType => WorkflowFileType;

    public int SchemaVersion
    {
        get => _schemaVersion;
        set => Set(ref _schemaVersion, Math.Max(1, value));
    }

    public string Id
    {
        get => _id;
        set => Set(ref _id, WorkflowIdentityRules.NormalizeId(value));
    }

    public string Name
    {
        get => _name;
        set => Set(ref _name, DisplayTextRules.CleanSingleLineLabel(value, "New Workflow"));
    }

    public string Category
    {
        get => _category;
        set => Set(ref _category, DisplayTextRules.CleanSingleLineLabel(value, "Custom"));
    }

    public string Description
    {
        get => _description;
        set => Set(ref _description, value ?? string.Empty);
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
