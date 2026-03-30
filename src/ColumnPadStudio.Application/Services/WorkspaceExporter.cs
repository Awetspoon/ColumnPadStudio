using System.Text;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.Application.Services;

public static class WorkspaceExporter
{
    public static string ToText(WorkspaceDocument workspace)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < workspace.Columns.Count; i++)
        {
            var column = workspace.Columns[i];
            if (i > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
            }

            sb.AppendLine($"===== {column.Title} =====");
            sb.AppendLine(column.Text);
        }

        return sb.ToString().TrimEnd();
    }

    public static string ToMarkdown(WorkspaceDocument workspace)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < workspace.Columns.Count; i++)
        {
            var column = workspace.Columns[i];
            if (i > 0)
            {
                sb.AppendLine();
                sb.AppendLine();
            }

            sb.AppendLine($"## {column.Title}");
            sb.AppendLine();
            sb.AppendLine(column.Text);
        }

        return sb.ToString().TrimEnd();
    }
}
