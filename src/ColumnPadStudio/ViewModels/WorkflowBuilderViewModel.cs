using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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

public sealed class WorkflowBuilderViewModel : NotifyBase
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

    public void Load()
    {
        Templates.Clear();
        foreach (var template in WorkflowTemplateCatalog.Templates)
            Templates.Add(template);

        SelectedTemplate = Templates.FirstOrDefault();

        Workflows.Clear();
        foreach (var workflow in _workflowService.LoadAll())
            Workflows.Add(workflow);

        if (Workflows.Count == 0)
        {
            NewWorkflow();
            StatusText = "No workflow files found yet. Created a new draft.";
            return;
        }

        SelectedWorkflow = Workflows[0];
        StatusText = $"Loaded {Workflows.Count} workflow(s).";
    }

    public void NewWorkflow()
    {
        var workflow = new WorkflowDefinition
        {
            Name = NextWorkflowName(),
            Category = "Custom",
            Trigger = WorkflowTriggerType.Manual,
            Description = ""
        };

        var start = new WorkflowDiagramNode
        {
            Id = "start",
            Kind = WorkflowNodeKind.Start,
            Title = "Start",
            X = 80,
            Y = 90,
            Width = 130,
            Height = 60
        };
        var step = new WorkflowDiagramNode
        {
            Id = "step-1",
            Kind = WorkflowNodeKind.Step,
            Title = "Step",
            X = 80,
            Y = 220
        };
        var end = new WorkflowDiagramNode
        {
            Id = "end",
            Kind = WorkflowNodeKind.End,
            Title = "End",
            X = 80,
            Y = 350,
            Width = 130,
            Height = 60
        };

        workflow.Nodes.Add(start);
        workflow.Nodes.Add(step);
        workflow.Nodes.Add(end);
        workflow.Links.Add(new WorkflowDiagramLink { FromNodeId = start.Id, ToNodeId = step.Id });
        workflow.Links.Add(new WorkflowDiagramLink { FromNodeId = step.Id, ToNodeId = end.Id });

        Workflows.Add(workflow);
        SelectedWorkflow = workflow;
        SelectedNode = step;
        StatusText = $"Created {workflow.Name}.";
    }

    public bool CreateWorkflowFromSelectedTemplate()
    {
        var template = SelectedTemplate;
        if (template is null)
            return false;

        var workflow = template.CreateWorkflowInstance(GetUniqueWorkflowName(template.Name));
        Workflows.Add(workflow);
        SelectedWorkflow = workflow;
        SelectedNode = workflow.Nodes.FirstOrDefault();
        StatusText = $"Created diagram from template: {template.Name}.";
        return true;
    }

    public bool ImportWorkflowFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        if (!_workflowService.TryLoad(filePath, out var imported))
            return false;

        var draft = _workflowService.CreateDraftFromImportedWorkflow(imported, filePath);
        draft.Name = GetUniqueWorkflowName(draft.Name);

        Workflows.Add(draft);
        SelectedWorkflow = draft;
        SelectedNode = draft.Nodes.FirstOrDefault();
        StatusText = $"Imported workflow from {Path.GetFileName(filePath)}.";
        return true;
    }

    public bool ExportSelectedWorkflowToFile(string filePath)
    {
        if (SelectedWorkflow is null || string.IsNullOrWhiteSpace(filePath))
            return false;

        _workflowService.ExportToPath(SelectedWorkflow, filePath);
        StatusText = $"Exported workflow JSON to {Path.GetFileName(filePath)}.";
        return true;
    }

    public void SaveSelectedWorkflow()
    {
        if (SelectedWorkflow is null)
            return;

        _workflowService.Save(SelectedWorkflow);
        OnPropertyChanged(nameof(SelectedWorkflowFileLabel));
        StatusText = $"Saved {Path.GetFileName(SelectedWorkflow.FilePath)}.";
    }

    public bool DeleteSelectedWorkflow()
    {
        var workflow = SelectedWorkflow;
        if (workflow is null)
            return false;

        var selectedIndex = Workflows.IndexOf(workflow);
        _workflowService.Delete(workflow);

        if (!Workflows.Remove(workflow))
            return false;

        if (Workflows.Count == 0)
        {
            NewWorkflow();
        }
        else
        {
            var nextIndex = Math.Clamp(selectedIndex, 0, Workflows.Count - 1);
            SelectedWorkflow = Workflows[nextIndex];
            StatusText = $"Deleted {workflow.Name}.";
        }

        return true;
    }

    public void AddNode()
    {
        if (SelectedWorkflow is null)
            return;

        var nodeIndex = SelectedWorkflow.Nodes.Count + 1;
        var reference = SelectedNode;

        var node = new WorkflowDiagramNode
        {
            Id = $"node-{nodeIndex}",
            Kind = WorkflowNodeKind.Step,
            Title = $"Step {nodeIndex}",
            X = reference?.X ?? 320,
            Y = (reference?.Y ?? 120) + 110
        };

        SelectedWorkflow.Nodes.Add(node);
        SelectedNode = node;
        OnPropertyChanged(nameof(CanCreateLink));
        StatusText = $"Added node {node.Title}.";
    }

    public bool DuplicateSelectedNode()
    {
        if (SelectedWorkflow is null || SelectedNode is null)
            return false;

        var clone = new WorkflowDiagramNode
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = SelectedNode.Kind,
            Title = $"{SelectedNode.Title} Copy",
            Description = SelectedNode.Description,
            X = SelectedNode.X + 36,
            Y = SelectedNode.Y + 36,
            Width = SelectedNode.Width,
            Height = SelectedNode.Height
        };

        SelectedWorkflow.Nodes.Add(clone);
        SelectedNode = clone;
        OnPropertyChanged(nameof(CanCreateLink));
        StatusText = "Node duplicated.";
        return true;
    }

    public bool RemoveSelectedNode()
    {
        if (SelectedWorkflow is null || SelectedNode is null)
            return false;

        var node = SelectedNode;
        var index = SelectedWorkflow.Nodes.IndexOf(node);
        if (index < 0)
            return false;

        for (var i = SelectedWorkflow.Links.Count - 1; i >= 0; i--)
        {
            var link = SelectedWorkflow.Links[i];
            if (string.Equals(link.FromNodeId, node.Id, StringComparison.Ordinal) ||
                string.Equals(link.ToNodeId, node.Id, StringComparison.Ordinal))
            {
                SelectedWorkflow.Links.RemoveAt(i);
            }
        }

        SelectedWorkflow.Nodes.RemoveAt(index);
        SelectedNode = SelectedWorkflow.Nodes.Count == 0
            ? null
            : SelectedWorkflow.Nodes[Math.Clamp(index, 0, SelectedWorkflow.Nodes.Count - 1)];

        if (SelectedLink is not null &&
            (!SelectedWorkflow.Links.Contains(SelectedLink)))
        {
            SelectedLink = SelectedWorkflow.Links.FirstOrDefault();
        }

        OnPropertyChanged(nameof(CanCreateLink));
        RefreshLinkPreviews();
        StatusText = "Node removed.";
        return true;
    }

    public bool NudgeSelectedNode(double dx, double dy)
    {
        if (SelectedNode is null)
            return false;

        SelectedNode.X = Math.Max(0, SelectedNode.X + dx);
        SelectedNode.Y = Math.Max(0, SelectedNode.Y + dy);
        RefreshLinkPreviews();
        return true;
    }

    public bool AutoLayoutSelectedWorkflow()
    {
        if (SelectedWorkflow is null || SelectedWorkflow.Nodes.Count == 0)
            return false;

        var ordered = SelectedWorkflow.Nodes
            .OrderBy(node => node.Kind == WorkflowNodeKind.Start ? 0 : node.Kind == WorkflowNodeKind.End ? 2 : 1)
            .ThenBy(node => node.Y)
            .ThenBy(node => node.X)
            .ToList();

        var y = 80.0;
        foreach (var node in ordered)
        {
            node.X = 80;
            node.Y = y;
            y += 110;
        }

        RefreshLinkPreviews();
        StatusText = "Auto-layout applied.";
        return true;
    }

    public bool AddLink()
    {
        if (SelectedWorkflow is null || SelectedWorkflow.Nodes.Count < 2)
            return false;

        var fromNode = SelectedNode ?? SelectedWorkflow.Nodes[0];
        var toNode = SelectedWorkflow.Nodes.FirstOrDefault(node => !string.Equals(node.Id, fromNode.Id, StringComparison.Ordinal))
                     ?? SelectedWorkflow.Nodes[0];

        var link = new WorkflowDiagramLink
        {
            FromNodeId = fromNode.Id,
            ToNodeId = toNode.Id
        };

        SelectedWorkflow.Links.Add(link);
        SelectedLink = link;
        RefreshLinkPreviews();
        StatusText = "Connection added.";
        return true;
    }

    public bool RemoveSelectedLink()
    {
        if (SelectedWorkflow is null || SelectedLink is null)
            return false;

        var index = SelectedWorkflow.Links.IndexOf(SelectedLink);
        if (index < 0)
            return false;

        SelectedWorkflow.Links.RemoveAt(index);
        SelectedLink = SelectedWorkflow.Links.Count == 0
            ? null
            : SelectedWorkflow.Links[Math.Clamp(index, 0, SelectedWorkflow.Links.Count - 1)];

        RefreshLinkPreviews();
        StatusText = "Connection removed.";
        return true;
    }

    public void RefreshLinkPreviews()
    {
        LinkPreviews.Clear();

        var workflow = SelectedWorkflow;
        if (workflow is null)
            return;

        var nodeLookup = workflow.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        foreach (var link in workflow.Links)
        {
            if (!nodeLookup.TryGetValue(link.FromNodeId, out var fromNode) ||
                !nodeLookup.TryGetValue(link.ToNodeId, out var toNode))
            {
                continue;
            }

            var x1 = fromNode.X + fromNode.Width;
            var y1 = fromNode.Y + (fromNode.Height / 2.0);
            var x2 = toNode.X;
            var y2 = toNode.Y + (toNode.Height / 2.0);

            var label = link.Label ?? string.Empty;
            LinkPreviews.Add(new WorkflowDiagramLinkPreview
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                LabelX = ((x1 + x2) / 2.0) + 4,
                LabelY = ((y1 + y2) / 2.0) - 12,
                Label = label,
                IsSelected = ReferenceEquals(link, SelectedLink)
            });
        }
    }

    private void SubscribeToWorkflow(WorkflowDefinition? workflow)
    {
        if (workflow is null)
            return;

        workflow.Nodes.CollectionChanged += WorkflowNodes_CollectionChanged;
        workflow.Links.CollectionChanged += WorkflowLinks_CollectionChanged;

        foreach (var node in workflow.Nodes)
            node.PropertyChanged += WorkflowNode_PropertyChanged;

        foreach (var link in workflow.Links)
            link.PropertyChanged += WorkflowLink_PropertyChanged;
    }

    private void UnsubscribeFromWorkflow(WorkflowDefinition? workflow)
    {
        if (workflow is null)
            return;

        workflow.Nodes.CollectionChanged -= WorkflowNodes_CollectionChanged;
        workflow.Links.CollectionChanged -= WorkflowLinks_CollectionChanged;

        foreach (var node in workflow.Nodes)
            node.PropertyChanged -= WorkflowNode_PropertyChanged;

        foreach (var link in workflow.Links)
            link.PropertyChanged -= WorkflowLink_PropertyChanged;
    }

    private void WorkflowNodes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<WorkflowDiagramNode>())
                item.PropertyChanged -= WorkflowNode_PropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<WorkflowDiagramNode>())
                item.PropertyChanged += WorkflowNode_PropertyChanged;
        }

        OnPropertyChanged(nameof(CanCreateLink));
        RefreshLinkPreviews();
    }

    private void WorkflowLinks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<WorkflowDiagramLink>())
                item.PropertyChanged -= WorkflowLink_PropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<WorkflowDiagramLink>())
                item.PropertyChanged += WorkflowLink_PropertyChanged;
        }

        RefreshLinkPreviews();
    }

    private void WorkflowNode_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkflowDiagramNode.X) or nameof(WorkflowDiagramNode.Y) or nameof(WorkflowDiagramNode.Width) or nameof(WorkflowDiagramNode.Height) or nameof(WorkflowDiagramNode.Title))
            RefreshLinkPreviews();
    }

    private void WorkflowLink_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkflowDiagramLink.FromNodeId) or nameof(WorkflowDiagramLink.ToNodeId) or nameof(WorkflowDiagramLink.Label))
            RefreshLinkPreviews();
    }

    private string NextWorkflowName()
    {
        return GetUniqueWorkflowName("Workflow");
    }

    private string GetUniqueWorkflowName(string baseName)
    {
        var normalizedBase = string.IsNullOrWhiteSpace(baseName)
            ? "Workflow"
            : baseName.Trim();

        if (!Workflows.Any(workflow => string.Equals(workflow.Name, normalizedBase, StringComparison.OrdinalIgnoreCase)))
            return normalizedBase;

        var index = 2;
        while (true)
        {
            var candidate = $"{normalizedBase} {index}";
            if (!Workflows.Any(workflow => string.Equals(workflow.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
            index++;
        }
    }
}
