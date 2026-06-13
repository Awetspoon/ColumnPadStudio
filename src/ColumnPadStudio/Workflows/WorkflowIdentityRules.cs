namespace ColumnPadStudio.Workflows;

public static class WorkflowIdentityRules
{
    public static string NormalizeId(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? Guid.NewGuid().ToString("N")
            : trimmed;
    }
}
