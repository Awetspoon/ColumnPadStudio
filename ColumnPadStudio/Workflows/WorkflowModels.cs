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

public enum WorkflowStepKind
{
    AddColumn,
    SetTheme,
    ToggleWordWrap,
    ToggleLineNumbers,
    SaveCurrentFile
}

public sealed class WorkflowStepDefinition : NotifyBase
{
    private WorkflowStepKind _kind = WorkflowStepKind.AddColumn;
    private string _argument = string.Empty;
    private string _notes = string.Empty;

    public WorkflowStepKind Kind
    {
        get => _kind;
        set
        {
            Set(ref _kind, value);
            OnPropertyChanged(nameof(Summary));
        }
    }

    public string Argument
    {
        get => _argument;
        set
        {
            Set(ref _argument, value ?? string.Empty);
            OnPropertyChanged(nameof(Summary));
        }
    }

    public string Notes
    {
        get => _notes;
        set => Set(ref _notes, value ?? string.Empty);
    }

    [JsonIgnore]
    public string Summary => string.IsNullOrWhiteSpace(Argument)
        ? Kind.ToString()
        : $"{Kind}: {Argument}";
}

public sealed class WorkflowDefinition : NotifyBase
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = "New Workflow";
    private string _description = string.Empty;
    private WorkflowTriggerType _trigger = WorkflowTriggerType.Manual;
    private ObservableCollection<WorkflowStepDefinition> _steps = new();

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

    public ObservableCollection<WorkflowStepDefinition> Steps
    {
        get => _steps;
        set => Set(ref _steps, value ?? new ObservableCollection<WorkflowStepDefinition>());
    }

    [JsonIgnore]
    public string? FilePath { get; set; }
}