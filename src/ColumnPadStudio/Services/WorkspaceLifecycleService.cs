using System.Linq;

namespace ColumnPadStudio.Services;

public static class WorkspaceLifecycleService
{
    private const string DefaultWorkspacePrefix = "Workspace";

    public static string NextWorkspaceName(IReadOnlyList<string> existingNames, string? prefix = null)
    {
        ArgumentNullException.ThrowIfNull(existingNames);

        var effectivePrefix = string.IsNullOrWhiteSpace(prefix)
            ? DefaultWorkspacePrefix
            : prefix.Trim();

        var index = 1;
        while (true)
        {
            var candidate = $"{effectivePrefix} {index}";
            if (!existingNames.Any(name => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;

            index++;
        }
    }

    public static bool CanCloseWorkspace(int workspaceCount)
        => workspaceCount > 1;

    public static int NextActiveWorkspaceIndexAfterClose(int closedWorkspaceIndex, int remainingWorkspaceCount)
    {
        if (remainingWorkspaceCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(remainingWorkspaceCount), "At least one workspace must remain open.");

        return Math.Clamp(closedWorkspaceIndex, 0, remainingWorkspaceCount - 1);
    }
}
