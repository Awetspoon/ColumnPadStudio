using System.IO;
using ColumnPadStudio.Services;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.ViewModels;

public sealed partial class WorkflowBuilderViewModel
{
    public void Load()
    {
        Templates.Clear();
        foreach (var template in WorkflowTemplateCatalog.Templates)
            Templates.Add(template);

        SelectedTemplate = Templates.FirstOrDefault();

        Workflows.Clear();
        _cleanWorkflowSignatures.Clear();
        foreach (var workflow in _workflowService.LoadAll())
        {
            Workflows.Add(workflow);
            MarkWorkflowClean(workflow);
        }

        if (Workflows.Count == 0)
        {
            AddWorkflow();
            if (SelectedWorkflow is not null)
                MarkWorkflowClean(SelectedWorkflow);
            StatusText = "No workflow files found yet. Added a blank draft.";
            return;
        }

        SelectedWorkflow = Workflows[0];

        StatusText = _workflowService.LastLoadWarnings.Count == 0
            ? $"Loaded {Workflows.Count} workflow(s)."
            : $"Loaded {Workflows.Count} workflow(s). Skipped {_workflowService.LastLoadWarnings.Count} unreadable file(s).";
    }

    public void AddWorkflow()
    {
        var workflow = WorkflowDefaults.CreateDefault(NextWorkflowName());

        Workflows.Add(workflow);
        SelectedWorkflow = workflow;
        SelectedNode = workflow.Nodes.FirstOrDefault(node => node.Kind == WorkflowNodeKind.Step);
        OnPropertyChanged(nameof(DiagramCanvasWidth));
        OnPropertyChanged(nameof(DiagramCanvasHeight));
        StatusText = $"Added {workflow.Name}.";
        NotifyWorkflowDirtyStateChanged();
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
        OnPropertyChanged(nameof(DiagramCanvasWidth));
        OnPropertyChanged(nameof(DiagramCanvasHeight));
        StatusText = $"Created diagram from template: {template.Name}.";
        NotifyWorkflowDirtyStateChanged();
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
        OnPropertyChanged(nameof(DiagramCanvasWidth));
        OnPropertyChanged(nameof(DiagramCanvasHeight));
        StatusText = $"Imported workflow from {Path.GetFileName(filePath)}.";
        NotifyWorkflowDirtyStateChanged();
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

    public bool ExportSelectedWorkflowTextToFile(string filePath)
    {
        if (SelectedWorkflow is null || string.IsNullOrWhiteSpace(filePath))
            return false;

        _workflowService.ExportTextToPath(SelectedWorkflow, filePath);
        StatusText = $"Exported workflow text to {Path.GetFileName(filePath)}.";
        return true;
    }

    public bool ExportSelectedWorkflowMarkdownToFile(string filePath)
    {
        if (SelectedWorkflow is null || string.IsNullOrWhiteSpace(filePath))
            return false;

        _workflowService.ExportMarkdownToPath(SelectedWorkflow, filePath);
        StatusText = $"Exported workflow markdown to {Path.GetFileName(filePath)}.";
        return true;
    }

    public void SaveSelectedWorkflow()
    {
        if (SelectedWorkflow is null)
            return;

        _workflowService.Save(SelectedWorkflow);
        MarkWorkflowClean(SelectedWorkflow);
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
        _cleanWorkflowSignatures.Remove(workflow);

        if (!Workflows.Remove(workflow))
            return false;

        if (Workflows.Count == 0)
        {
            AddWorkflow();
        }
        else
        {
            var nextIndex = Math.Clamp(selectedIndex, 0, Workflows.Count - 1);
            SelectedWorkflow = Workflows[nextIndex];
            StatusText = $"Deleted {workflow.Name}.";
        }

        return true;
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
