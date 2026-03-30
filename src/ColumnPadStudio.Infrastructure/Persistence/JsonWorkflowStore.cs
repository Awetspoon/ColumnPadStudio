using System.Text.Json;
using ColumnPadStudio.Application.Abstractions;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.Infrastructure.Persistence;

public sealed class JsonWorkflowStore : IWorkflowStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _baseFolder;

    public JsonWorkflowStore()
    {
        _baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ColumnPadStudio",
            "Workflows");
        Directory.CreateDirectory(_baseFolder);
    }

    public async Task<IReadOnlyList<WorkflowDefinition>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        var files = Directory.GetFiles(_baseFolder, "*.workflow.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = new List<WorkflowDefinition>();
        foreach (var file in files)
        {
            var workflow = await LoadAsync(file, cancellationToken);
            if (workflow is not null)
            {
                items.Add(workflow);
            }
        }

        return items;
    }

    public async Task<WorkflowDefinition?> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var workflow = await JsonSerializer.DeserializeAsync<WorkflowDefinition>(stream, Options, cancellationToken);
        if (workflow is null)
        {
            return null;
        }

        workflow.FilePath = path;
        return workflow;
    }

    public async Task<string> SaveAsync(WorkflowDefinition workflow, string? path = null, CancellationToken cancellationToken = default)
    {
        var resolvedPath = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(_baseFolder, SanitizeFileName(workflow.Name) + ".workflow.json")
            : path;

        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        workflow.FilePath = resolvedPath;
        await using var stream = File.Create(resolvedPath);
        await JsonSerializer.SerializeAsync(stream, workflow, Options, cancellationToken);
        return resolvedPath;
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var name = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(name) ? "workflow" : name;
    }
}
