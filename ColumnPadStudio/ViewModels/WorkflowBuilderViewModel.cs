using System.Collections.ObjectModel;
using System.IO;
using ColumnPadStudio.Services;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.ViewModels;

public sealed class WorkflowBuilderViewModel : NotifyBase
{
    private readonly WorkflowService _workflowService;
    private WorkflowDefinition? _selectedWorkflow;
    private WorkflowStepDefinition? _selectedStep;
    private string _statusText = "Ready.";

    public ObservableCollection<WorkflowDefinition> Workflows { get; } = new();

    public IReadOnlyList<WorkflowTriggerType> TriggerTypes { get; } = Enum.GetValues<WorkflowTriggerType>();
    public IReadOnlyList<WorkflowStepKind> StepKinds { get; } = Enum.GetValues<WorkflowStepKind>();

    public WorkflowDefinition? SelectedWorkflow
    {
        get => _selectedWorkflow;
        set
        {
            if (ReferenceEquals(_selectedWorkflow, value))
                return;

            _selectedWorkflow = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedWorkflow));
            OnPropertyChanged(nameof(SelectedWorkflowFileLabel));

            SelectedStep = _selectedWorkflow?.Steps.FirstOrDefault();
        }
    }

    public WorkflowStepDefinition? SelectedStep
    {
        get => _selectedStep;
        set
        {
            if (ReferenceEquals(_selectedStep, value))
                return;

            _selectedStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedStep));
        }
    }

    public bool HasSelectedWorkflow => SelectedWorkflow is not null;
    public bool HasSelectedStep => SelectedStep is not null;

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
            Trigger = WorkflowTriggerType.Manual
        };

        workflow.Steps.Add(new WorkflowStepDefinition
        {
            Kind = WorkflowStepKind.AddColumn,
            Notes = "Start step"
        });

        Workflows.Add(workflow);
        SelectedWorkflow = workflow;
        StatusText = $"Created {workflow.Name}.";
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

    public void AddStep()
    {
        if (SelectedWorkflow is null)
            return;

        var step = new WorkflowStepDefinition
        {
            Kind = WorkflowStepKind.AddColumn
        };

        SelectedWorkflow.Steps.Add(step);
        SelectedStep = step;
        StatusText = $"Added step {SelectedWorkflow.Steps.Count}.";
    }

    public bool RemoveSelectedStep()
    {
        if (SelectedWorkflow is null || SelectedStep is null)
            return false;

        var steps = SelectedWorkflow.Steps;
        var index = steps.IndexOf(SelectedStep);
        if (index < 0)
            return false;

        steps.RemoveAt(index);
        if (steps.Count == 0)
            SelectedStep = null;
        else
            SelectedStep = steps[Math.Clamp(index, 0, steps.Count - 1)];

        StatusText = "Step removed.";
        return true;
    }

    public bool MoveSelectedStepUp()
    {
        return MoveSelectedStep(-1);
    }

    public bool MoveSelectedStepDown()
    {
        return MoveSelectedStep(+1);
    }

    private bool MoveSelectedStep(int delta)
    {
        if (SelectedWorkflow is null || SelectedStep is null)
            return false;

        var steps = SelectedWorkflow.Steps;
        var currentIndex = steps.IndexOf(SelectedStep);
        if (currentIndex < 0)
            return false;

        var targetIndex = currentIndex + delta;
        if (targetIndex < 0 || targetIndex >= steps.Count)
            return false;

        (steps[currentIndex], steps[targetIndex]) = (steps[targetIndex], steps[currentIndex]);
        SelectedStep = steps[targetIndex];
        StatusText = "Step order updated.";
        return true;
    }

    private string NextWorkflowName()
    {
        var index = 1;
        while (true)
        {
            var candidate = $"Workflow {index}";
            if (!Workflows.Any(w => string.Equals(w.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
            index++;
        }
    }
}
