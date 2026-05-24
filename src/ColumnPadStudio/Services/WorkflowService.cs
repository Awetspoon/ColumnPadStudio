using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.Services;

public sealed class WorkflowService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string DefaultWorkflowsDirectory => AppStoragePaths.WorkflowsDirectory;

    public string WorkflowsDirectory { get; }

    public WorkflowService(string? workflowsDirectory = null)
    {
        WorkflowsDirectory = string.IsNullOrWhiteSpace(workflowsDirectory)
            ? DefaultWorkflowsDirectory
            : workflowsDirectory;
    }

    public IReadOnlyList<WorkflowDefinition> LoadAll()
    {
        if (!Directory.Exists(WorkflowsDirectory))
            return Array.Empty<WorkflowDefinition>();

        var loaded = new List<WorkflowDefinition>();
        foreach (var filePath in Directory
                     .GetFiles(WorkflowsDirectory, "*.workflow.json")
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (TryLoad(filePath, out var workflow))
                loaded.Add(workflow);
        }

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
        workflow.SchemaVersion = Math.Max(2, workflow.SchemaVersion);

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

}

