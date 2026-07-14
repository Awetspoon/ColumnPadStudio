using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.Services;

public sealed partial class WorkflowService
{
    public const string TextExportMarker = "ColumnPad Workflow Export";
    public const string TextExportFormatLine = "Format: Text";
    public const string MarkdownExportMarker = "<!-- ColumnPad Workflow Export: Markdown -->";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string DefaultWorkflowsDirectory => AppStoragePaths.WorkflowsDirectory;

    public string WorkflowsDirectory { get; }
    public IReadOnlyList<string> LastLoadWarnings { get; private set; } = [];

    public WorkflowService(string? workflowsDirectory = null)
    {
        WorkflowsDirectory = string.IsNullOrWhiteSpace(workflowsDirectory)
            ? DefaultWorkflowsDirectory
            : workflowsDirectory;
    }

    public static bool IsWorkflowDefinitionJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var root = document.RootElement;
            if (TryGetPropertyIgnoreCase(root, nameof(WorkflowDefinition.FileType), out var fileType) &&
                fileType.ValueKind == JsonValueKind.String &&
                !string.Equals(fileType.GetString(), WorkflowDefinition.WorkflowFileType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryGetPropertyIgnoreCase(root, nameof(WorkflowDefinition.Nodes), out var nodes) &&
                   nodes.ValueKind == JsonValueKind.Array &&
                   TryGetPropertyIgnoreCase(root, nameof(WorkflowDefinition.Links), out var links) &&
                   links.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public IReadOnlyList<WorkflowDefinition> LoadAll()
    {
        if (!Directory.Exists(WorkflowsDirectory))
        {
            LastLoadWarnings = [];
            return Array.Empty<WorkflowDefinition>();
        }

        var loaded = new List<WorkflowDefinition>();
        var warnings = new List<string>();
        foreach (var filePath in Directory
                     .GetFiles(WorkflowsDirectory, "*.workflow.json")
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (TryLoad(filePath, out var workflow))
                loaded.Add(workflow);
            else
                warnings.Add(Path.GetFileName(filePath));
        }

        LastLoadWarnings = warnings;
        return loaded;
    }

    public bool TryLoad(string filePath, out WorkflowDefinition workflow)
    {
        workflow = new WorkflowDefinition();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        try
        {
            var json = File.ReadAllText(filePath);
            if (!IsWorkflowDefinitionJson(json))
                return false;

            var parsed = JsonSerializer.Deserialize<WorkflowDefinition>(json, JsonOptions);
            if (parsed is null)
                return false;

            Normalize(parsed, Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(filePath)));
            parsed.FilePath = filePath;
            workflow = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Save(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        Normalize(workflow, fallbackName: null);

        Directory.CreateDirectory(WorkflowsDirectory);

        var path = string.IsNullOrWhiteSpace(workflow.FilePath)
            ? BuildWorkflowFilePath(workflow.Name, workflow.Id)
            : workflow.FilePath!;

        var serializableCopy = Snapshot(workflow);
        var json = JsonSerializer.Serialize(serializableCopy, JsonOptions);
        AtomicFileWriter.WriteText(path, json);

        workflow.FilePath = path;
    }

    public string CreateContentSignature(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        return JsonSerializer.Serialize(Snapshot(workflow), JsonOptions);
    }

    public void Delete(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        if (string.IsNullOrWhiteSpace(workflow.FilePath))
            return;

        if (File.Exists(workflow.FilePath))
            File.Delete(workflow.FilePath);

        workflow.FilePath = null;
    }

    public void ExportToPath(WorkflowDefinition workflow, string filePath)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        Normalize(workflow, fallbackName: null);
        var serializableCopy = Snapshot(workflow);
        var json = JsonSerializer.Serialize(serializableCopy, JsonOptions);
        AtomicFileWriter.WriteText(filePath, json);
    }

    public void ExportTextToPath(WorkflowDefinition workflow, string filePath)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        AtomicFileWriter.WriteText(filePath, BuildTextExport(workflow), Encoding.UTF8);
    }

    public void ExportMarkdownToPath(WorkflowDefinition workflow, string filePath)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        AtomicFileWriter.WriteText(filePath, BuildMarkdownExport(workflow), Encoding.UTF8);
    }

    public WorkflowDefinition CreateDraftFromImportedWorkflow(WorkflowDefinition imported, string? sourceLabel = null)
    {
        ArgumentNullException.ThrowIfNull(imported);

        var draft = Snapshot(imported);
        draft.Id = Guid.NewGuid().ToString("N");
        draft.FilePath = null;

        var fallbackName = string.IsNullOrWhiteSpace(sourceLabel)
            ? imported.Name
            : Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(sourceLabel));
        Normalize(draft, fallbackName);

        if (!draft.Name.EndsWith(" (imported)", StringComparison.OrdinalIgnoreCase))
            draft.Name = $"{draft.Name} (imported)";

        return draft;
    }

    private string BuildWorkflowFilePath(string? name, string? id)
    {
        var safeName = SanitizeFileName(name);
        var shortId = string.IsNullOrWhiteSpace(id)
            ? Guid.NewGuid().ToString("N")[..8]
            : id.Length >= 8 ? id[..8] : id;

        return Path.Combine(WorkflowsDirectory, $"{safeName}-{shortId}.workflow.json");
    }

    private static string SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "workflow";

        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Trim().Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
        cleaned = cleaned.Replace(' ', '-');
        return string.IsNullOrWhiteSpace(cleaned) ? "workflow" : cleaned;
    }

    private static WorkflowDefinition Snapshot(WorkflowDefinition source)
    {
        return new WorkflowDefinition
        {
            SchemaVersion = source.SchemaVersion,
            Id = source.Id,
            Name = source.Name,
            Category = source.Category,
            Description = source.Description,
            Trigger = source.Trigger,
            Nodes = new ObservableCollection<WorkflowDiagramNode>(
                source.Nodes.Select(node => new WorkflowDiagramNode
                {
                    Id = node.Id,
                    Kind = node.Kind,
                    Title = node.Title,
                    Description = node.Description,
                    Goal = node.Goal,
                    Instructions = node.Instructions,
                    ExpectedOutput = node.ExpectedOutput,
                    ChecklistItems = CopyChecklistItems(node.ChecklistItems),
                    X = node.X,
                    Y = node.Y,
                    Width = node.Width,
                    Height = node.Height,
                    Color = node.Color
                })),
            Links = new ObservableCollection<WorkflowDiagramLink>(
                source.Links.Select(link => new WorkflowDiagramLink
                {
                    Id = link.Id,
                    FromNodeId = link.FromNodeId,
                    ToNodeId = link.ToNodeId,
                    Label = link.Label
                }))
        };
    }

    private static void Normalize(WorkflowDefinition workflow, string? fallbackName)
    {
        workflow.SchemaVersion = Math.Max(3, workflow.SchemaVersion);

        workflow.Id = string.IsNullOrWhiteSpace(workflow.Id)
            ? Guid.NewGuid().ToString("N")
            : workflow.Id.Trim();

        workflow.Name = string.IsNullOrWhiteSpace(workflow.Name)
            ? string.IsNullOrWhiteSpace(fallbackName) ? "New Workflow" : fallbackName.Trim()
            : workflow.Name.Trim();

        workflow.Category = string.IsNullOrWhiteSpace(workflow.Category)
            ? "Custom"
            : workflow.Category.Trim();

        workflow.Description ??= string.Empty;
        workflow.Nodes ??= [];
        workflow.Links ??= [];

        if (workflow.Nodes.Count == 0)
            WorkflowDefaults.PopulateStarterDiagram(workflow);

        EnsureUniqueNodeIds(workflow.Nodes);
        NormalizeNodeContent(workflow.Nodes);
        EnsureUniqueLinkIds(workflow.Links);

        var nodeIds = new HashSet<string>(workflow.Nodes.Select(n => n.Id), StringComparer.Ordinal);
        for (var i = workflow.Links.Count - 1; i >= 0; i--)
        {
            var link = workflow.Links[i];
            if (string.IsNullOrWhiteSpace(link.FromNodeId) ||
                string.IsNullOrWhiteSpace(link.ToNodeId) ||
                !nodeIds.Contains(link.FromNodeId) ||
                !nodeIds.Contains(link.ToNodeId))
            {
                workflow.Links.RemoveAt(i);
            }
        }
    }

    private static ObservableCollection<WorkflowChecklistItem> CopyChecklistItems(IEnumerable<WorkflowChecklistItem>? items)
    {
        return new ObservableCollection<WorkflowChecklistItem>(
            (items ?? Array.Empty<WorkflowChecklistItem>())
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .Select(item => new WorkflowChecklistItem
            {
                Text = item.Text.Trim(),
                IsDone = item.IsDone
            }));
    }

    private static void EnsureUniqueNodeIds(IEnumerable<WorkflowDiagramNode> nodes)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var candidate = string.IsNullOrWhiteSpace(node.Id) ? Guid.NewGuid().ToString("N") : node.Id.Trim();
            while (!used.Add(candidate))
                candidate = Guid.NewGuid().ToString("N");

            node.Id = candidate;

            if (string.IsNullOrWhiteSpace(node.Title))
                node.Title = WorkflowDiagramNode.DefaultTitleForKind(node.Kind);
        }
    }

    private static void NormalizeNodeContent(IEnumerable<WorkflowDiagramNode> nodes)
    {
        foreach (var node in nodes)
        {
            node.ChecklistItems = CopyChecklistItems(node.ChecklistItems);
        }
    }

    private static void EnsureUniqueLinkIds(IEnumerable<WorkflowDiagramLink> links)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in links)
        {
            var candidate = string.IsNullOrWhiteSpace(link.Id) ? Guid.NewGuid().ToString("N") : link.Id.Trim();
            while (!used.Add(candidate))
                candidate = Guid.NewGuid().ToString("N");

            link.Id = candidate;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

}

