using System.Collections.Specialized;
using System.ComponentModel;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.ViewModels;

public sealed partial class WorkflowBuilderViewModel
{
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
        NotifyDiagramCanvasSizeChanged();
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
        if (e.PropertyName is nameof(WorkflowDiagramNode.X) or nameof(WorkflowDiagramNode.Y) or nameof(WorkflowDiagramNode.Width) or nameof(WorkflowDiagramNode.Height))
        {
            NotifyDiagramCanvasSizeChanged();
            RefreshLinkPreviews();
            return;
        }

        if (e.PropertyName == nameof(WorkflowDiagramNode.Title))
            RefreshLinkPreviews();
    }

    private void WorkflowLink_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkflowDiagramLink.FromNodeId) or nameof(WorkflowDiagramLink.ToNodeId) or nameof(WorkflowDiagramLink.Label))
            RefreshLinkPreviews();
    }

    private void NotifyDiagramCanvasSizeChanged()
    {
        OnPropertyChanged(nameof(DiagramCanvasWidth));
        OnPropertyChanged(nameof(DiagramCanvasHeight));
    }
}
