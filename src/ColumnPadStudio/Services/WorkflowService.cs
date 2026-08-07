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

            var parsed = DeserializeWorkflow(json);
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

}

