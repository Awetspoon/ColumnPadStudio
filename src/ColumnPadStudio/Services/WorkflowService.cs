using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using ColumnPadStudio.ViewModels;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.Services;

public sealed class WorkflowService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string DefaultWorkflowsDirectory => Path.Combine(MainViewModel.AutoSaveDirectory, "Workflows");

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
        WriteTextAtomically(path, json);

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
        WriteTextAtomically(filePath, json);
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
            Steps = new ObservableCollection<WorkflowStepDefinition>(
                source.Steps.Select(step => new WorkflowStepDefinition
                {
                    Kind = step.Kind,
                    Argument = step.Argument,
                    Notes = step.Notes
                }))
        };
    }

    private static void Normalize(WorkflowDefinition workflow, string? fallbackName)
    {
        workflow.SchemaVersion = Math.Max(1, workflow.SchemaVersion);

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
        workflow.Steps ??= new ObservableCollection<WorkflowStepDefinition>();
    }

    private static void WriteTextAtomically(string path, string content)
    {
        var tempPath = $"{path}.tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, path, overwrite: true);
    }
}

