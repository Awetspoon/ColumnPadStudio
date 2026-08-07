using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.ViewModels;

public sealed partial class WorkflowBuilderViewModel
{
    private WorkflowDiagramNode? _connectionFromNode;
    private WorkflowDiagramNode? _connectionToNode;
    private string _connectionLabel = string.Empty;

    public WorkflowDiagramNode? ConnectionFromNode
    {
        get => _connectionFromNode;
        set
        {
            if (ReferenceEquals(_connectionFromNode, value))
                return;

            Set(ref _connectionFromNode, value);
            OnPropertyChanged(nameof(CanCreateLink));
        }
    }

    public WorkflowDiagramNode? ConnectionToNode
    {
        get => _connectionToNode;
        set
        {
            if (ReferenceEquals(_connectionToNode, value))
                return;

            Set(ref _connectionToNode, value);
            OnPropertyChanged(nameof(CanCreateLink));
        }
    }

    public string ConnectionLabel
    {
        get => _connectionLabel;
        set => Set(ref _connectionLabel, value ?? string.Empty);
    }

    private bool HasValidConnectionDraft()
    {
        var workflow = SelectedWorkflow;
        var fromNode = ConnectionFromNode;
        var toNode = ConnectionToNode;

        if (workflow is null ||
            fromNode is null ||
            toNode is null ||
            ReferenceEquals(fromNode, toNode) ||
            !workflow.Nodes.Contains(fromNode) ||
            !workflow.Nodes.Contains(toNode))
        {
            return false;
        }

        return !workflow.Links.Any(link =>
            string.Equals(link.FromNodeId, fromNode.Id, StringComparison.Ordinal) &&
            string.Equals(link.ToNodeId, toNode.Id, StringComparison.Ordinal));
    }

    private bool AddConnectionFromDraft()
    {
        if (!HasValidConnectionDraft() || SelectedWorkflow is null)
        {
            StatusText = "Choose two different nodes that are not already connected.";
            return false;
        }

        var fromNode = ConnectionFromNode!;
        var toNode = ConnectionToNode!;
        var link = new WorkflowDiagramLink
        {
            FromNodeId = fromNode.Id,
            ToNodeId = toNode.Id,
            Label = ConnectionLabel.Trim()
        };

        SelectedWorkflow.Links.Add(link);
        SelectedLink = link;
        ConnectionFromNode = toNode;
        ConnectionToNode = null;
        ConnectionLabel = string.Empty;
        StatusText = $"Connected {fromNode.Title} to {toNode.Title}.";
        return true;
    }

    private void ResetConnectionDraft()
    {
        ConnectionFromNode = null;
        ConnectionToNode = null;
        ConnectionLabel = string.Empty;
    }

    private void UseSelectedNodeAsConnectionStart()
    {
        if (ConnectionToNode is null)
            ConnectionFromNode = SelectedNode;
    }

    private void ValidateConnectionDraftNodes()
    {
        var workflow = SelectedWorkflow;
        if (workflow is null)
        {
            ResetConnectionDraft();
            return;
        }

        if (ConnectionFromNode is not null && !workflow.Nodes.Contains(ConnectionFromNode))
            ConnectionFromNode = SelectedNode is not null && workflow.Nodes.Contains(SelectedNode) ? SelectedNode : workflow.Nodes.FirstOrDefault();

        if (ConnectionToNode is not null && !workflow.Nodes.Contains(ConnectionToNode))
            ConnectionToNode = null;

        OnPropertyChanged(nameof(CanCreateLink));
    }
}
