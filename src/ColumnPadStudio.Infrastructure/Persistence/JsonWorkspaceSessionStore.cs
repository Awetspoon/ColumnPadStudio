using System.Text.Json;
using ColumnPadStudio.Application.Abstractions;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.Infrastructure.Persistence;

public sealed class JsonWorkspaceSessionStore : IWorkspaceSessionStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task SaveAsync(string path, WorkspaceSessionDocument session, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, session, Options, cancellationToken);
    }

    public async Task<WorkspaceSessionDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var session = await JsonSerializer.DeserializeAsync<WorkspaceSessionDocument>(stream, Options, cancellationToken);
        return session ?? new WorkspaceSessionDocument();
    }
}
