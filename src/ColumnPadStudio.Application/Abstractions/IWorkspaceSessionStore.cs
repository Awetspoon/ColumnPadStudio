using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.Application.Abstractions;

public interface IWorkspaceSessionStore
{
    Task SaveAsync(string path, WorkspaceSessionDocument session, CancellationToken cancellationToken = default);
    Task<WorkspaceSessionDocument> LoadAsync(string path, CancellationToken cancellationToken = default);
}
