using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using ColumnPadStudio.Domain.Text;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.Services;

public sealed class WorkflowService
{
    public const string TextExportMarker = "ColumnPad Workflow Export";
    public const string TextExportFormatLine = "Format: Text";
    public const string MarkdownExportMarker = "<!-- ColumnPad Workflow Export: Markdown -->";

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
            return root.TryGetProperty(nameof(WorkflowDefinition.Nodes), out var nodes) &&
                   nodes.ValueKind == JsonValueKind.Array &&
                   root.TryGetProperty(nameof(WorkflowDefinition.Links), out var links) &&
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

    public string BuildTextExport(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        Normalize(workflow, fallbackName: null);
        var export = Snapshot(workflow);
        var orderedNodes = GetReadableNodeOrder(export);
        var nodeIndexes = BuildNodeIndexes(orderedNodes);

        var sb = new StringBuilder();
        sb.AppendLine(TextExportMarker);
        sb.AppendLine(TextExportFormatLine);
        sb.AppendLine();
        sb.AppendLine($"Workflow: {CleanSingleLine(export.Name, "New Workflow")}");
        sb.AppendLine($"Category: {CleanSingleLine(export.Category, "Custom")}");
        sb.AppendLine($"Trigger: {export.Trigger}");
        AppendTextBlock(sb, "Description", export.Description);

        sb.AppendLine();
        sb.AppendLine("Steps");
        sb.AppendLine("-----");

        foreach (var node in orderedNodes)
        {
            sb.AppendLine($"{nodeIndexes[node.Id]}. [{node.Kind}] {CleanSingleLine(node.Title, WorkflowDiagramNode.DefaultTitleForKind(node.Kind))}");
            AppendTextBlock(sb, "Description", node.Description);
            AppendTextBlock(sb, "Goal", node.Goal);
            AppendTextBlock(sb, "Instructions", node.Instructions);
            AppendTextBlock(sb, "Expected output", node.ExpectedOutput);
            AppendTextChecklist(sb, node);
            AppendTextNextSteps(sb, export, node, nodeIndexes);
            sb.AppendLine();
        }

        AppendTextConnections(sb, export, nodeIndexes);
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    public string BuildMarkdownExport(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        Normalize(workflow, fallbackName: null);
        var export = Snapshot(workflow);
        var orderedNodes = GetReadableNodeOrder(export);
        var nodeIndexes = BuildNodeIndexes(orderedNodes);

        var sb = new StringBuilder();
        sb.AppendLine(MarkdownExportMarker);
        sb.AppendLine();
        sb.AppendLine($"# {EscapeMarkdownInline(CleanSingleLine(export.Name, "New Workflow"))}");
        sb.AppendLine();
        sb.AppendLine($"**Category:** {EscapeMarkdownInline(CleanSingleLine(export.Category, "Custom"))}");
        sb.AppendLine();
        sb.AppendLine($"**Trigger:** {export.Trigger}");
        AppendMarkdownBlock(sb, "Description", export.Description);

        sb.AppendLine();
        sb.AppendLine("## Steps");

        foreach (var node in orderedNodes)
        {
            sb.AppendLine();
            sb.AppendLine($"### {nodeIndexes[node.Id]}. {node.Kind}: {EscapeMarkdownInline(CleanSingleLine(node.Title, WorkflowDiagramNode.DefaultTitleForKind(node.Kind)))}");
            AppendMarkdownBlock(sb, "Description", node.Description);
            AppendMarkdownBlock(sb, "Goal", node.Goal);
            AppendMarkdownBlock(sb, "Instructions", node.Instructions);
            AppendMarkdownBlock(sb, "Expected output", node.ExpectedOutput);
            AppendMarkdownChecklist(sb, node);
            AppendMarkdownNextSteps(sb, export, node, nodeIndexes);
        }

        AppendMarkdownConnections(sb, export, nodeIndexes);
        return sb.ToString().TrimEnd() + Environment.NewLine;
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

    private static List<WorkflowDiagramNode> GetReadableNodeOrder(WorkflowDefinition workflow)
    {
        var nodes = workflow.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .ToList();
        var nodeById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var outgoingLinks = workflow.Links
            .Where(link => nodeById.ContainsKey(link.FromNodeId) && nodeById.ContainsKey(link.ToNodeId))
            .GroupBy(link => link.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var ordered = new List<WorkflowDiagramNode>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var startNodes = nodes
            .Where(node => node.Kind == WorkflowNodeKind.Start)
            .OrderBy(node => node.X)
            .ThenBy(node => node.Y)
            .ToList();

        if (startNodes.Count == 0)
        {
            var incomingIds = workflow.Links
                .Where(link => nodeById.ContainsKey(link.ToNodeId))
                .Select(link => link.ToNodeId)
                .ToHashSet(StringComparer.Ordinal);

            startNodes = nodes
                .Where(node => !incomingIds.Contains(node.Id))
                .OrderBy(node => node.X)
                .ThenBy(node => node.Y)
                .ToList();
        }

        if (startNodes.Count == 0)
        {
            startNodes = nodes
                .OrderBy(node => node.X)
                .ThenBy(node => node.Y)
                .ToList();
        }

        foreach (var node in startNodes)
            AddConnectedNodes(node);

        foreach (var node in nodes.OrderBy(node => node.X).ThenBy(node => node.Y))
            AddConnectedNodes(node);

        return ordered;

        void AddConnectedNodes(WorkflowDiagramNode firstNode)
        {
            var queue = new Queue<WorkflowDiagramNode>();
            queue.Enqueue(firstNode);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (!visited.Add(node.Id))
                    continue;

                ordered.Add(node);

                if (!outgoingLinks.TryGetValue(node.Id, out var links))
                    continue;

                foreach (var link in links
                             .Select(link => new { Link = link, Target = nodeById[link.ToNodeId] })
                             .OrderBy(item => item.Target.X)
                             .ThenBy(item => item.Target.Y)
                             .ThenBy(item => item.Link.Label, StringComparer.OrdinalIgnoreCase))
                {
                    queue.Enqueue(link.Target);
                }
            }
        }
    }

    private static Dictionary<string, int> BuildNodeIndexes(IReadOnlyList<WorkflowDiagramNode> orderedNodes)
    {
        return orderedNodes
            .Select((node, index) => new { node.Id, Number = index + 1 })
            .ToDictionary(item => item.Id, item => item.Number, StringComparer.Ordinal);
    }

    private static void AppendTextBlock(StringBuilder sb, string label, string? value)
    {
        var text = CleanMultiline(value);
        if (text.Length == 0)
            return;

        sb.AppendLine($"{label}:");
        foreach (var line in text.Split('\n'))
            sb.AppendLine($"  {line}");
    }

    private static void AppendTextChecklist(StringBuilder sb, WorkflowDiagramNode node)
    {
        var items = node.ChecklistItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .ToList();

        if (items.Count == 0)
            return;

        sb.AppendLine("Checklist:");
        foreach (var item in items)
        {
            var marker = item.IsDone ? "[x]" : "[ ]";
            sb.AppendLine($"  - {marker} {CleanSingleLine(item.Text, "Checklist item")}");
        }
    }

    private static void AppendTextNextSteps(
        StringBuilder sb,
        WorkflowDefinition workflow,
        WorkflowDiagramNode node,
        IReadOnlyDictionary<string, int> nodeIndexes)
    {
        var links = GetOutgoingLinks(workflow, node.Id).ToList();
        if (links.Count == 0)
            return;

        var nodesById = workflow.Nodes.ToDictionary(item => item.Id, StringComparer.Ordinal);

        sb.AppendLine("Next:");
        foreach (var link in links)
        {
            if (!nodesById.TryGetValue(link.ToNodeId, out var target))
                continue;

            var label = string.IsNullOrWhiteSpace(link.Label)
                ? string.Empty
                : $" ({CleanSingleLine(link.Label, string.Empty)})";
            sb.AppendLine($"  - {BuildTextNodeReference(target, nodeIndexes)}{label}");
        }
    }

    private static void AppendTextConnections(
        StringBuilder sb,
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, int> nodeIndexes)
    {
        var nodesById = workflow.Nodes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var links = workflow.Links
            .Where(link => nodesById.ContainsKey(link.FromNodeId) && nodesById.ContainsKey(link.ToNodeId))
            .ToList();

        sb.AppendLine("Connections");
        sb.AppendLine("-----------");

        if (links.Count == 0)
        {
            sb.AppendLine("No connections.");
            return;
        }

        foreach (var link in links)
        {
            var from = BuildTextNodeReference(nodesById[link.FromNodeId], nodeIndexes);
            var to = BuildTextNodeReference(nodesById[link.ToNodeId], nodeIndexes);
            var label = string.IsNullOrWhiteSpace(link.Label)
                ? string.Empty
                : $" ({CleanSingleLine(link.Label, string.Empty)})";
            sb.AppendLine($"- {from} -> {to}{label}");
        }
    }

    private static void AppendMarkdownBlock(StringBuilder sb, string label, string? value)
    {
        var text = CleanMultiline(value);
        if (text.Length == 0)
            return;

        sb.AppendLine();
        sb.AppendLine($"**{label}:**");
        sb.AppendLine();
        sb.AppendLine(text);
    }

    private static void AppendMarkdownChecklist(StringBuilder sb, WorkflowDiagramNode node)
    {
        var items = node.ChecklistItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .ToList();

        if (items.Count == 0)
            return;

        sb.AppendLine();
        sb.AppendLine("**Checklist:**");
        sb.AppendLine();
        foreach (var item in items)
        {
            var marker = item.IsDone ? "[x]" : "[ ]";
            sb.AppendLine($"- {marker} {item.Text.Trim()}");
        }
    }

    private static void AppendMarkdownNextSteps(
        StringBuilder sb,
        WorkflowDefinition workflow,
        WorkflowDiagramNode node,
        IReadOnlyDictionary<string, int> nodeIndexes)
    {
        var links = GetOutgoingLinks(workflow, node.Id).ToList();
        if (links.Count == 0)
            return;

        var nodesById = workflow.Nodes.ToDictionary(item => item.Id, StringComparer.Ordinal);

        sb.AppendLine();
        sb.AppendLine("**Next:**");
        sb.AppendLine();
        foreach (var link in links)
        {
            if (!nodesById.TryGetValue(link.ToNodeId, out var target))
                continue;

            var label = string.IsNullOrWhiteSpace(link.Label)
                ? string.Empty
                : $" ({EscapeMarkdownInline(CleanSingleLine(link.Label, string.Empty))})";
            sb.AppendLine($"- {EscapeMarkdownInline(BuildTextNodeReference(target, nodeIndexes))}{label}");
        }
    }

    private static void AppendMarkdownConnections(
        StringBuilder sb,
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, int> nodeIndexes)
    {
        var nodesById = workflow.Nodes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var links = workflow.Links
            .Where(link => nodesById.ContainsKey(link.FromNodeId) && nodesById.ContainsKey(link.ToNodeId))
            .ToList();

        sb.AppendLine();
        sb.AppendLine("## Connections");
        sb.AppendLine();

        if (links.Count == 0)
        {
            sb.AppendLine("No connections.");
            return;
        }

        foreach (var link in links)
        {
            var from = EscapeMarkdownInline(BuildTextNodeReference(nodesById[link.FromNodeId], nodeIndexes));
            var to = EscapeMarkdownInline(BuildTextNodeReference(nodesById[link.ToNodeId], nodeIndexes));
            var label = string.IsNullOrWhiteSpace(link.Label)
                ? string.Empty
                : $" ({EscapeMarkdownInline(CleanSingleLine(link.Label, string.Empty))})";
            sb.AppendLine($"- **{from}** -> **{to}**{label}");
        }
    }

    private static IEnumerable<WorkflowDiagramLink> GetOutgoingLinks(WorkflowDefinition workflow, string nodeId)
    {
        var nodesById = workflow.Nodes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        return workflow.Links
            .Where(link => string.Equals(link.FromNodeId, nodeId, StringComparison.Ordinal) &&
                           nodesById.ContainsKey(link.ToNodeId))
            .OrderBy(link => nodesById[link.ToNodeId].X)
            .ThenBy(link => nodesById[link.ToNodeId].Y)
            .ThenBy(link => link.Label, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildTextNodeReference(
        WorkflowDiagramNode node,
        IReadOnlyDictionary<string, int> nodeIndexes)
    {
        var title = CleanSingleLine(node.Title, WorkflowDiagramNode.DefaultTitleForKind(node.Kind));
        return nodeIndexes.TryGetValue(node.Id, out var number)
            ? $"{number}. {title}"
            : title;
    }

    private static string CleanSingleLine(string? value, string fallback)
    {
        return DisplayTextRules.CleanSingleLineLabel(value, fallback);
    }

    private static string CleanMultiline(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    private static string EscapeMarkdownInline(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
    }

}

