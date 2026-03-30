using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColumnPadStudio.Application.Abstractions;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Models;
using ColumnPadStudio.Infrastructure.Persistence;

namespace ColumnPadStudio.App.Views;

public partial class WorkflowBuilderWindow : Window
{
    private readonly IWorkflowStore _workflowStore = new JsonWorkflowStore();
    private readonly ObservableCollection<WorkflowDefinition> _library = new();
    private readonly ObservableCollection<WorkflowDefinition> _templates = new();
    private readonly ObservableCollection<WorkflowNode> _nodes = new();
    private readonly ObservableCollection<WorkflowLinkListItem> _links = new();
    private bool _suppressUiSync;
    private WorkflowNode? _draggingNode;
    private Point _dragStartPoint;
    private Point _dragStartOrigin;
    private bool _dragMoved;

    private WorkflowDefinition _currentWorkflow = CreateBlankWorkflow();

    public WorkflowBuilderWindow()
    {
        InitializeComponent();
        WorkflowLibraryListBox.ItemsSource = _library;
        TemplateComboBox.ItemsSource = _templates;
        NodesListBox.ItemsSource = _nodes;
        LinksListBox.ItemsSource = _links;
        Loaded += WorkflowBuilderWindow_OnLoaded;
    }

    private WorkflowNode? SelectedNode => NodesListBox.SelectedItem as WorkflowNode;
    private WorkflowLinkListItem? SelectedLink => LinksListBox.SelectedItem as WorkflowLinkListItem;

    private void ApplyWorkflowToUi(WorkflowDefinition workflow)
    {
        _currentWorkflow = workflow;

        _suppressUiSync = true;
        WorkflowNameTextBox.Text = workflow.Name;
        CategoryTextBox.Text = workflow.Category;
        DescriptionTextBox.Text = workflow.Description;
        SelectComboItemByTag(TriggerComboBox, workflow.Trigger);
        SelectComboItemByTag(ChartStyleComboBox, workflow.ChartStyle);

        _nodes.Clear();
        foreach (var node in workflow.Nodes.OrderBy(n => n.X).ThenBy(n => n.Y))
        {
            _nodes.Add(node);
        }

        RefreshLinksList();
        NodesListBox.SelectedItem = _nodes.FirstOrDefault();
        UpdateSelectedNodeUi();
        UpdateWorkflowSummaryUi();
        _suppressUiSync = false;
        RenderWorkflow();
        SetStatus($"Loaded workflow: {workflow.Name}");
    }

    private void RefreshLinksList()
    {
        _links.Clear();
        foreach (var link in _currentWorkflow.Links)
        {
            var from = _currentWorkflow.Nodes.FirstOrDefault(n => n.Id == link.FromNodeId)?.Title ?? "Unknown";
            var to = _currentWorkflow.Nodes.FirstOrDefault(n => n.Id == link.ToNodeId)?.Title ?? "Unknown";
            _links.Add(new WorkflowLinkListItem(link, $"{from} -> {to}"));
        }

        UpdateWorkflowSummaryUi();
    }

    private void WorkflowDetails_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSync)
        {
            return;
        }

        var previousChartStyle = _currentWorkflow.ChartStyle;
        _currentWorkflow.Name = string.IsNullOrWhiteSpace(WorkflowNameTextBox.Text) ? "New Workflow" : WorkflowNameTextBox.Text.Trim();
        _currentWorkflow.Category = string.IsNullOrWhiteSpace(CategoryTextBox.Text) ? "General" : CategoryTextBox.Text.Trim();
        _currentWorkflow.Description = DescriptionTextBox.Text.Trim();
        _currentWorkflow.Trigger = GetComboTag(TriggerComboBox, WorkflowTriggerType.Manual);
        _currentWorkflow.ChartStyle = GetComboTag(ChartStyleComboBox, WorkflowChartStyle.Flowchart);
        WorkflowLibraryListBox.Items.Refresh();
        UpdateWorkflowSummaryUi();

        if (_currentWorkflow.ChartStyle != previousChartStyle)
        {
            ApplyCurrentChartStyleLayout($"Chart style set to {GetChartStyleDisplayName(_currentWorkflow.ChartStyle)}.");
        }
    }

    private void NodeDetails_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressUiSync || SelectedNode is null)
        {
            return;
        }

        SelectedNode.Title = string.IsNullOrWhiteSpace(NodeTitleTextBox.Text) ? SelectedNode.Kind.ToString() : NodeTitleTextBox.Text.Trim();
        SelectedNode.Description = NodeDescriptionTextBox.Text.Trim();
        SelectedNode.Kind = GetComboTag(NodeKindComboBox, WorkflowNodeKind.Step);
        NodesListBox.Items.Refresh();
        RefreshLinksList();
        UpdateSelectedNodeAccent();
        RenderWorkflow();
    }

    private void WorkflowLibraryListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUiSync || WorkflowLibraryListBox.SelectedItem is not WorkflowDefinition workflow)
        {
            return;
        }

        ApplyWorkflowToUi(CloneWorkflow(workflow));
    }

    private void NodesListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedNodeUi();
        RenderWorkflow();
    }

    private void LinksListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SelectedLink is not null)
        {
            var fromNode = _currentWorkflow.Nodes.FirstOrDefault(node => node.Id == SelectedLink.Link.FromNodeId);
            if (fromNode is not null && !ReferenceEquals(NodesListBox.SelectedItem, fromNode))
            {
                NodesListBox.SelectedItem = fromNode;
            }
        }

        RenderWorkflow();
    }

    private void UpdateSelectedNodeUi()
    {
        _suppressUiSync = true;
        if (SelectedNode is null)
        {
            NodeTitleTextBox.Text = string.Empty;
            NodeDescriptionTextBox.Text = string.Empty;
            NodeKindComboBox.SelectedIndex = -1;
            RefreshLinkTargetChoices();
            LinkTargetComboBox.SelectedIndex = -1;
        }
        else
        {
            NodeTitleTextBox.Text = SelectedNode.Title;
            NodeDescriptionTextBox.Text = SelectedNode.Description;
            SelectComboItemByTag(NodeKindComboBox, SelectedNode.Kind);
            RefreshLinkTargetChoices();
            var linkedTargetId = _currentWorkflow.Links.FirstOrDefault(link => link.FromNodeId == SelectedNode.Id)?.ToNodeId;
            var availableTargets = (IEnumerable<WorkflowNode>)LinkTargetComboBox.ItemsSource;
            LinkTargetComboBox.SelectedItem = availableTargets.FirstOrDefault(node => node.Id == linkedTargetId)
                ?? availableTargets.FirstOrDefault();
        }

        _suppressUiSync = false;
        UpdateSelectedNodeAccent();
    }

    private static void SelectComboItemByTag(ComboBox comboBox, object tag)
    {
        foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
        {
            if (Equals(item.Tag, tag))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        comboBox.SelectedIndex = -1;
    }

    private static TEnum GetComboTag<TEnum>(ComboBox comboBox, TEnum fallback) where TEnum : struct
    {
        if (comboBox.SelectedItem is ComboBoxItem { Tag: TEnum value })
        {
            return value;
        }

        return fallback;
    }

    private void SelectLibraryItemByPath(string path)
    {
        var match = _library.FirstOrDefault(item => string.Equals(item.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            _suppressUiSync = true;
            WorkflowLibraryListBox.SelectedItem = match;
            _suppressUiSync = false;
        }
    }

    private static Brush GetThemeBrush(string key)
    {
        return System.Windows.Application.Current.Resources[key] as Brush ?? Brushes.Transparent;
    }

    private static string GetChartStyleDisplayName(WorkflowChartStyle chartStyle)
    {
        return chartStyle switch
        {
            WorkflowChartStyle.Flowchart => "Flowchart",
            WorkflowChartStyle.HorizontalTimeline => "Horizontal Timeline",
            WorkflowChartStyle.VerticalTimeline => "Vertical Timeline",
            WorkflowChartStyle.Swimlane => "Swimlane",
            WorkflowChartStyle.Kanban => "Kanban Board",
            WorkflowChartStyle.RadialMap => "Radial Map",
            _ => chartStyle.ToString()
        };
    }

    private void RefreshLinkTargetChoices()
    {
        LinkTargetComboBox.ItemsSource = SelectedNode is null
            ? Array.Empty<WorkflowNode>()
            : _nodes.Where(node => node.Id != SelectedNode.Id).ToList();
    }

    private void UpdateWorkflowSummaryUi()
    {
        if (WorkflowSummaryTextBlock is null)
        {
            return;
        }

        WorkflowSummaryTextBlock.Text = $"{_currentWorkflow.Nodes.Count} {Pluralize("node", _currentWorkflow.Nodes.Count)} • {_currentWorkflow.Links.Count} {Pluralize("line", _currentWorkflow.Links.Count)} • {GetChartStyleDisplayName(_currentWorkflow.ChartStyle)}";
    }

    private static string Pluralize(string singular, int count)
    {
        return count == 1 ? singular : singular + "s";
    }

    private void SetStatus(string text) => StatusTextBlock.Text = text;

    private sealed record WorkflowLinkListItem(WorkflowLink Link, string Display)
    {
        public override string ToString() => Display;
    }
}
