using System.Text.Json;
using ColumnPadStudio.Application.Abstractions;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.Infrastructure.Persistence;

public sealed class AppDataRecoveryStore : IRecoveryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _snapshotPath;

    public AppDataRecoveryStore()
    {
        var baseFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ColumnPadStudio",
            "Recovery");

        Directory.CreateDirectory(baseFolder);
        _snapshotPath = Path.Combine(baseFolder, "recovery.columnpad.json");
    }

    public async Task SaveSnapshotAsync(RecoverySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        snapshot.SavedAtLocal = DateTime.Now;
        var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
        await File.WriteAllTextAsync(_snapshotPath, json, cancellationToken);
    }

    public async Task<RecoverySnapshot?> TryLoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_snapshotPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_snapshotPath, cancellationToken);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Recovery backup root must be an object.");
            }

            if (document.RootElement.TryGetProperty("session", out _))
            {
                var snapshot = JsonSerializer.Deserialize<RecoverySnapshot>(json, SerializerOptions) ?? new RecoverySnapshot();
                if (snapshot.SavedAtLocal == default)
                {
                    snapshot.SavedAtLocal = File.GetLastWriteTime(_snapshotPath);
                }

                snapshot.Session ??= new WorkspaceSessionDocument();
                snapshot.WorkspaceStates ??= new List<RecoveryWorkspaceState>();
                return snapshot;
            }

            var legacySession = JsonSerializer.Deserialize<WorkspaceSessionDocument>(json, SerializerOptions) ?? new WorkspaceSessionDocument();
            return new RecoverySnapshot
            {
                Session = legacySession,
                SavedAtLocal = File.GetLastWriteTime(_snapshotPath)
            };
        }
        catch
        {
            if (File.Exists(_snapshotPath))
            {
                File.Delete(_snapshotPath);
            }

            return null;
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_snapshotPath))
        {
            File.Delete(_snapshotPath);
        }

        return Task.CompletedTask;
    }
}
