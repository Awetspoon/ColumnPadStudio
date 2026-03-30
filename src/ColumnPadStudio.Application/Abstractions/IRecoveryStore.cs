using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.Application.Abstractions;

public interface IRecoveryStore
{
    Task SaveSnapshotAsync(RecoverySnapshot snapshot, CancellationToken cancellationToken = default);
    Task<RecoverySnapshot?> TryLoadSnapshotAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
