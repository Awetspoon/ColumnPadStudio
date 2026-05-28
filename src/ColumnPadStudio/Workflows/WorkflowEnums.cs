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

public enum WorkflowNodeColor
{
    Auto,
    Blue,
    Green,
    Amber,
    Rose,
    Slate
}
