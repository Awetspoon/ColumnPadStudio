using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.Services;

public sealed partial class WorkflowService
{
    private static WorkflowDefinition? DeserializeWorkflow(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var hasLegacySteps = TryGetPropertyIgnoreCase(root, "Steps", out var steps) &&
                             steps.ValueKind == JsonValueKind.Array;
        var hasCurrentNodes = TryGetPropertyIgnoreCase(root, nameof(WorkflowDefinition.Nodes), out var nodes) &&
                              nodes.ValueKind == JsonValueKind.Array;

        return hasLegacySteps && !hasCurrentNodes
            ? MigrateLegacyStepWorkflow(root, steps)
            : JsonSerializer.Deserialize<WorkflowDefinition>(json, JsonOptions);
    }

    private static WorkflowDefinition MigrateLegacyStepWorkflow(JsonElement root, JsonElement steps)
    {
        var workflow = new WorkflowDefinition
        {
            SchemaVersion = WorkflowDefinition.CurrentSchemaVersion,
            Id = ReadLegacyString(root, nameof(WorkflowDefinition.Id), Guid.NewGuid().ToString("N")),
            Name = ReadLegacyString(root, nameof(WorkflowDefinition.Name), "Imported Workflow"),
            Category = ReadLegacyString(root, nameof(WorkflowDefinition.Category), "Imported"),
            Description = ReadLegacyString(root, nameof(WorkflowDefinition.Description), string.Empty),
            Nodes = [],
            Links = []
        };

        workflow.Nodes.Add(new WorkflowDiagramNode
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = WorkflowNodeKind.Start,
            Title = "Start",
            Description = workflow.Description,
            Goal = "Begin the imported workflow.",
            Instructions = "Review the original workflow steps in order.",
            ExpectedOutput = "The workflow is ready to begin.",
            X = 120,
            Y = 80
        });

        var stepIndex = 0;
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object)
                continue;

            stepIndex++;
            var kindName = ReadLegacyStepKind(step);
            var title = LegacyStepTitle(kindName, stepIndex);
            var argument = ReadLegacyString(step, "Argument", string.Empty);
            var notes = ReadLegacyString(step, "Notes", string.Empty);
            var instructions = string.IsNullOrWhiteSpace(argument)
                ? notes
                : string.IsNullOrWhiteSpace(notes)
                    ? $"Setting or value: {argument}"
                    : $"Setting or value: {argument}{Environment.NewLine}{notes}";

            workflow.Nodes.Add(new WorkflowDiagramNode
            {
                Id = Guid.NewGuid().ToString("N"),
                Kind = WorkflowNodeKind.Step,
                Title = title,
                Description = notes,
                Goal = $"Complete the {title.ToLowerInvariant()} action from the original workflow.",
                Instructions = instructions,
                ExpectedOutput = $"{title} completed.",
                X = 120,
                Y = 80 + (stepIndex * 130)
            });
        }

        workflow.Nodes.Add(new WorkflowDiagramNode
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = WorkflowNodeKind.End,
            Title = "End",
            Goal = "Finish the imported workflow.",
            Instructions = "Confirm that each original step has been completed.",
            ExpectedOutput = "The workflow is complete.",
            X = 120,
            Y = 80 + ((stepIndex + 1) * 130)
        });

        for (var index = 0; index < workflow.Nodes.Count - 1; index++)
        {
            workflow.Links.Add(new WorkflowDiagramLink
            {
                Id = Guid.NewGuid().ToString("N"),
                FromNodeId = workflow.Nodes[index].Id,
                ToNodeId = workflow.Nodes[index + 1].Id
            });
        }

        return workflow;
    }

    private static string ReadLegacyStepKind(JsonElement step)
    {
        if (!TryGetPropertyIgnoreCase(step, "Kind", out var kind))
            return "Step";

        if (kind.ValueKind == JsonValueKind.String)
            return kind.GetString() ?? "Step";

        if (kind.ValueKind == JsonValueKind.Number && kind.TryGetInt32(out var numericKind))
        {
            return numericKind switch
            {
                0 => "AddColumn",
                1 => "SetTheme",
                2 => "ToggleWordWrap",
                3 => "ToggleLineNumbers",
                4 => "SaveCurrentFile",
                5 => "SetColumnCount",
                6 => "SetSpellCheck",
                7 => "SetEditorLanguage",
                8 => "SetLinedPaper",
                _ => "Step"
            };
        }

        return "Step";
    }

    private static string LegacyStepTitle(string kindName, int index)
    {
        return kindName.ToUpperInvariant() switch
        {
            "ADDCOLUMN" => "Add column",
            "SETTHEME" => "Set theme",
            "TOGGLEWORDWRAP" => "Set word wrap",
            "TOGGLELINENUMBERS" => "Set line numbers",
            "SAVECURRENTFILE" => "Save current file",
            "SETCOLUMNCOUNT" => "Set column count",
            "SETSPELLCHECK" => "Set spell check",
            "SETEDITORLANGUAGE" => "Set proofing language",
            "SETLINEDPAPER" => "Set paper style",
            _ => HumanizeLegacyStepKind(kindName, index)
        };
    }

    private static string HumanizeLegacyStepKind(string value, int index)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "Step", StringComparison.OrdinalIgnoreCase))
            return $"Step {index}";

        var builder = new StringBuilder(value.Length + 8);
        for (var characterIndex = 0; characterIndex < value.Length; characterIndex++)
        {
            var character = value[characterIndex];
            if (characterIndex > 0 && char.IsUpper(character) && char.IsLower(value[characterIndex - 1]))
                builder.Append(' ');

            builder.Append(character);
        }

        var result = builder.ToString().Trim();
        return result.Length == 0
            ? $"Step {index}"
            : char.ToUpperInvariant(result[0]) + result[1..];
    }

    private static string ReadLegacyString(JsonElement element, string propertyName, string fallback)
    {
        return TryGetPropertyIgnoreCase(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }
}
