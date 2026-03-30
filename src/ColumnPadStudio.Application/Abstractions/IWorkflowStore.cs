using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.Application.Abstractions;

public interface IWorkflowStore
{
    Task<IReadOnlyList<WorkflowDefinition>> LoadAllAsync(CancellationToken cancellationToken = default);
    Task<WorkflowDefinition?> LoadAsync(string path, CancellationToken cancellationToken = default);
    Task<string> SaveAsync(WorkflowDefinition workflow, string? path = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
