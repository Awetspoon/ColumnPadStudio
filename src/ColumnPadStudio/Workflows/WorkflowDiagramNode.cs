using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json.Serialization;
using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio.Workflows;

public sealed class WorkflowDiagramNode : NotifyBase
{
    private string _id = Guid.NewGuid().ToString("N");
    private WorkflowNodeKind _kind = WorkflowNodeKind.Step;
    private string _title = "Step";
    private string _description = string.Empty;
    private string _goal = string.Empty;
    private string _instructions = string.Empty;
    private string _expectedOutput = string.Empty;
    private ObservableCollection<WorkflowChecklistItem> _checklistItems = [];
    private double _x = 80;
    private double _y = 80;
    private double _width = 170;
    private double _height = 72;
    private WorkflowNodeColor _color = WorkflowNodeColor.Auto;
    private bool _isSelected;

    public WorkflowDiagramNode()
    {
        _checklistItems.CollectionChanged += ChecklistItems_CollectionChanged;
    }

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

    public string Goal
    {
        get => _goal;
        set => Set(ref _goal, value ?? string.Empty);
    }

    public string Instructions
    {
        get => _instructions;
        set => Set(ref _instructions, value ?? string.Empty);
    }

    public string ExpectedOutput
    {
        get => _expectedOutput;
        set => Set(ref _expectedOutput, value ?? string.Empty);
    }

    public ObservableCollection<WorkflowChecklistItem> ChecklistItems
    {
        get => _checklistItems;
        set
        {
            if (ReferenceEquals(_checklistItems, value))
                return;

            _checklistItems.CollectionChanged -= ChecklistItems_CollectionChanged;
            _checklistItems = value ?? [];
            _checklistItems.CollectionChanged += ChecklistItems_CollectionChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ChecklistText));
        }
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

    public WorkflowNodeColor Color
    {
        get => _color;
        set => Set(ref _color, value);
    }

    [JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    [JsonIgnore]
    public string ChecklistText
    {
        get => string.Join(Environment.NewLine, ChecklistItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .Select(item => item.IsDone ? $"- [x] {item.Text.Trim()}" : $"- [ ] {item.Text.Trim()}"));
        set
        {
            ChecklistItems = ParseChecklistText(value);
            OnPropertyChanged();
        }
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

    private static ObservableCollection<WorkflowChecklistItem> ParseChecklistText(string? value)
    {
        var items = new ObservableCollection<WorkflowChecklistItem>();
        if (string.IsNullOrWhiteSpace(value))
            return items;

        foreach (var rawLine in value.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var text = rawLine.Trim();
            if (text.Length == 0)
                continue;

            var isDone = false;
            if (text.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase))
            {
                isDone = true;
                text = text[6..].Trim();
            }
            else if (text.StartsWith("- [ ] ", StringComparison.OrdinalIgnoreCase))
            {
                text = text[6..].Trim();
            }

            if (text.Length > 0)
                items.Add(new WorkflowChecklistItem { Text = text, IsDone = isDone });
        }

        return items;
    }

    private void ChecklistItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ChecklistText));
    }
}
