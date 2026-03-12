using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio.Workflows;

public enum WorkflowTriggerType
{
    Manual,
    OnAppStart,
    OnFileOpen,
    OnFileSave
}

public enum WorkflowNodeKind
{
    Start,
    Step,
    Decision,
    End,
    Note
}


public sealed class WorkflowDiagramNode : NotifyBase
{
    private string _id = Guid.NewGuid().ToString("N");
    private WorkflowNodeKind _kind = WorkflowNodeKind.Step;
    private string _title = "Step";
    private string _description = string.Empty;
    private double _x = 80;
    private double _y = 80;
    private double _width = 170;
    private double _height = 72;
    private bool _isSelected;

    public string Id
    {
        get => _id;
        set => Set(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
    }

    public WorkflowNodeKind Kind
    {
        get => _kind;
        set
        {
            Set(ref _kind, value);
            OnPropertyChanged(nameof(Summary));
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            Set(ref _title, string.IsNullOrWhiteSpace(value) ? DefaultTitleForKind(Kind) : value.Trim());
            OnPropertyChanged(nameof(Summary));
        }
    }

    public string Description
    {
        get => _description;
        set => Set(ref _description, value ?? string.Empty);
    }

    public double X
    {
        get => _x;
        set => Set(ref _x, Math.Max(0, Math.Round(value, 1)));
    }

    public double Y
    {
        get => _y;
        set => Set(ref _y, Math.Max(0, Math.Round(value, 1)));
    }

    public double Width
    {
        get => _width;
        set => Set(ref _width, Math.Clamp(Math.Round(value, 1), 100, 360));
    }

    public double Height
    {
        get => _height;
        set => Set(ref _height, Math.Clamp(Math.Round(value, 1), 46, 240));
    }

    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    [JsonIgnore]
    public string Summary => $"{Kind}: {Title}";

    public static string DefaultTitleForKind(WorkflowNodeKind kind)
        => kind switch
        {
            WorkflowNodeKind.Start => "Start",
            WorkflowNodeKind.Step => "Step",
            WorkflowNodeKind.Decision => "Decision",
            WorkflowNodeKind.End => "End",
            WorkflowNodeKind.Note => "Note",
            _ => "Node"
        };
}

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
        set => Set(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value.Trim());
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

public sealed class WorkflowDefinition : NotifyBase
{
    private int _schemaVersion = 2;
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


