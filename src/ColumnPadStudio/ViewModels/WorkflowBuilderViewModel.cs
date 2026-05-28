using System.Collections.ObjectModel;
using System.IO;
using ColumnPadStudio.Services;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.ViewModels;

public sealed class WorkflowDiagramLinkPreview
{
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
    public double LabelX { get; init; }
    public double LabelY { get; init; }
    public string Label { get; init; } = string.Empty;
    public bool IsSelected { get; init; }
}

public sealed partial class WorkflowBuilderViewModel : NotifyBase
{
    private readonly WorkflowService _workflowService;
    private WorkflowDefinition? _selectedWorkflow;
    private WorkflowDiagramNode? _selectedNode;
    private WorkflowDiagramLink? _selectedLink;
    private WorkflowTemplateDefinition? _selectedTemplate;
    private string _statusText = "Ready.";

    public ObservableCollection<WorkflowDefinition> Workflows { get; } = [];
    public ObservableCollection<WorkflowTemplateDefinition> Templates { get; } = [];
    public ObservableCollection<WorkflowDiagramLinkPreview> LinkPreviews { get; } = [];

    public IReadOnlyList<WorkflowTriggerType> TriggerTypes { get; } = Enum.GetValues<WorkflowTriggerType>();
    public IReadOnlyList<WorkflowNodeKind> NodeKinds { get; } = Enum.GetValues<WorkflowNodeKind>();
    public IReadOnlyList<WorkflowNodeColor> NodeColors { get; } = Enum.GetValues<WorkflowNodeColor>();

    public WorkflowDefinition? SelectedWorkflow
    {
        get => _selectedWorkflow;
        set
        {
            if (ReferenceEquals(_selectedWorkflow, value))
                return;

            UnsubscribeFromWorkflow(_selectedWorkflow);
            _selectedWorkflow = value;
            SubscribeToWorkflow(_selectedWorkflow);

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedWorkflow));
            OnPropertyChanged(nameof(CanCreateLink));
            OnPropertyChanged(nameof(SelectedWorkflowFileLabel));

            SelectedNode = _selectedWorkflow?.Nodes.FirstOrDefault();
            SelectedLink = _selectedWorkflow?.Links.FirstOrDefault();
            RefreshLinkPreviews();
        }
    }

    public WorkflowDiagramNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (ReferenceEquals(_selectedNode, value))
                return;

            if (_selectedNode is not null)
                _selectedNode.IsSelected = false;

            _selectedNode = value;

            if (_selectedNode is not null)
                _selectedNode.IsSelected = true;

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedNode));
        }
    }

    public WorkflowDiagramLink? SelectedLink
    {
        get => _selectedLink;
        set
        {
            if (ReferenceEquals(_selectedLink, value))
                return;

            if (_selectedLink is not null)
                _selectedLink.IsSelected = false;

            _selectedLink = value;

            if (_selectedLink is not null)
                _selectedLink.IsSelected = true;

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedLink));
            RefreshLinkPreviews();
        }
    }

    public WorkflowTemplateDefinition? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (ReferenceEquals(_selectedTemplate, value))
                return;

            _selectedTemplate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedTemplate));
        }
    }

    public bool HasSelectedWorkflow => SelectedWorkflow is not null;
    public bool HasSelectedNode => SelectedNode is not null;
    public bool HasSelectedLink => SelectedLink is not null;
    public bool HasSelectedTemplate => SelectedTemplate is not null;
    public bool CanCreateLink => SelectedWorkflow is { Nodes.Count: >= 2 };
    public double DiagramCanvasWidth => CalculateDiagramCanvasWidth();
    public double DiagramCanvasHeight => CalculateDiagramCanvasHeight();

    public string SelectedWorkflowFileLabel
    {
        get
        {
            if (SelectedWorkflow is null || string.IsNullOrWhiteSpace(SelectedWorkflow.FilePath))
                return "Not yet saved";

            return Path.GetFileName(SelectedWorkflow.FilePath);
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    public WorkflowBuilderViewModel(WorkflowService workflowService)
    {
        _workflowService = workflowService;
    }

    private double CalculateDiagramCanvasWidth()
    {
        var workflow = SelectedWorkflow;
        if (workflow is null || workflow.Nodes.Count == 0)
            return 980;

        return Math.Max(980, workflow.Nodes.Max(node => node.X + node.Width) + 96);
    }

    private double CalculateDiagramCanvasHeight()
    {
        var workflow = SelectedWorkflow;
        if (workflow is null || workflow.Nodes.Count == 0)
            return 620;

        return Math.Max(620, workflow.Nodes.Max(node => node.Y + node.Height) + 96);
    }
}
