using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using ColumnPadStudio.Workflows;
using System.Collections.ObjectModel;
using System.IO;

namespace ColumnPadStudio.SmokeTests;

internal static class WorkflowSmokeTests
{
    public static WorkflowDefinition Run(SmokeTestContext tests, string layoutJson)
    {
        var workflowTemp = Path.Combine(Path.GetTempPath(), $"columnpad-workflows-{Guid.NewGuid():N}");
        var workflowDefinition = new WorkflowDefinition { Name = "Colour test" };

        try
        {
            var workflowService = new WorkflowService(workflowTemp);
            var emptyWorkflowVm = new WorkflowBuilderViewModel(workflowService);
            emptyWorkflowVm.Load();
            tests.Check(emptyWorkflowVm.Workflows.Count == 1, "Workflow Builder should create one workflow when no saved workflows exist.");
            tests.Check(WorkflowTemplateCatalog.Templates.Count >= 10, "Workflow starter catalog should provide multiple practical starters.");
            var workflowTemplateIds = WorkflowTemplateCatalog.Templates.Select(template => template.Id).ToList();
            tests.Check(workflowTemplateIds.Count == workflowTemplateIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Workflow starter catalog should not contain duplicate IDs.");
            tests.Check(WorkflowTemplateCatalog.Templates.All(template => template.Nodes.Count > 0), "Workflow starter catalog should not contain empty starter diagrams.");
            tests.Check(WorkflowTemplateCatalog.Templates.All(template => template.Connections.Count > 0), "Workflow starter catalog should wire starter nodes together.");
            var essayStarter = WorkflowTemplateCatalog.Templates.FirstOrDefault(template => template.Id == "essay-plan");
            tests.Check(essayStarter is not null, "Workflow starter catalog should include an essay planning starter.");
            if (essayStarter is not null)
            {
                var essayWorkflow = essayStarter.CreateWorkflowInstance("Essay Plan Copy");
                tests.Check(essayWorkflow.Name == "Essay Plan Copy", "Workflow starter instances should allow a custom workflow name.");
                tests.Check(essayWorkflow.Nodes.Count >= 5, "Workflow starter instances should create a useful editable diagram.");
                tests.Check(essayWorkflow.Links.Count > 0, "Workflow starter instances should create connections between starter nodes.");
                var thesisNode = essayWorkflow.Nodes.FirstOrDefault(node => node.Title == "Define thesis");
                tests.Check(!string.IsNullOrWhiteSpace(thesisNode?.Goal), "Workflow starter nodes should include a real goal, not just a box title.");
                tests.Check(thesisNode?.ChecklistItems.Count >= 2, "Workflow starter nodes should include useful checklist data.");
            }

            var workflowBuilderVm = new WorkflowBuilderViewModel(workflowService);
            workflowBuilderVm.AddWorkflow();
            workflowBuilderVm.AddNode(WorkflowNodeKind.Decision);
            tests.Check(workflowBuilderVm.SelectedNode?.Kind == WorkflowNodeKind.Decision, "Workflow builder palette should add the requested node kind.");
            var firstDecision = workflowBuilderVm.SelectedNode!;
            tests.Check(firstDecision.Title == "Decision", "The first added Decision should use the clean default title without a misleading workflow-wide number.");
            tests.Check(!OverlapsAnyNode(firstDecision, workflowBuilderVm.SelectedWorkflow!.Nodes), "A newly added Decision should not overlap an existing workflow node.");

            workflowBuilderVm.AddNode(WorkflowNodeKind.Decision);
            var secondDecision = workflowBuilderVm.SelectedNode!;
            tests.Check(secondDecision.Title == "Decision 2", "The second added Decision should use the next number for that node kind.");
            tests.Check(!OverlapsAnyNode(secondDecision, workflowBuilderVm.SelectedWorkflow.Nodes), "A second added Decision should be placed without overlapping an existing workflow node.");

            tests.Check(workflowBuilderVm.DuplicateSelectedNode(), "Workflow builder should duplicate the selected node.");
            var duplicatedDecision = workflowBuilderVm.SelectedNode!;
            tests.Check(workflowBuilderVm.SelectedWorkflow.Nodes.Count(node => string.Equals(node.Title, duplicatedDecision.Title, StringComparison.OrdinalIgnoreCase)) == 1,
                "A duplicated node should receive a unique title.");
            tests.Check(!OverlapsAnyNode(duplicatedDecision, workflowBuilderVm.SelectedWorkflow.Nodes), "A duplicated node should be placed without overlapping an existing workflow node.");

            workflowBuilderVm.SelectedWorkflow.Nodes[0].Height = 180;
            tests.Check(workflowBuilderVm.AutoLayoutSelectedWorkflow(), "Workflow builder should tidy the selected workflow positions.");
            tests.Check(!ContainsOverlappingNodes(workflowBuilderVm.SelectedWorkflow.Nodes), "Workflow position tidying should account for node heights and leave every node non-overlapping.");

            var connectionCountBeforeDraft = workflowBuilderVm.SelectedWorkflow.Links.Count;
            workflowBuilderVm.ConnectionFromNode = null;
            workflowBuilderVm.ConnectionToNode = null;
            workflowBuilderVm.ConnectionLabel = "Decision route";
            tests.Check(!workflowBuilderVm.CanCreateLink, "A connection should require both a From node and a To node.");
            tests.Check(!workflowBuilderVm.AddLink() && workflowBuilderVm.SelectedWorkflow.Links.Count == connectionCountBeforeDraft,
                "Adding a connection should fail without explicit endpoints.");

            workflowBuilderVm.ConnectionFromNode = firstDecision;
            tests.Check(!workflowBuilderVm.CanCreateLink && !workflowBuilderVm.AddLink(), "A connection should not be created when only its From node is selected.");
            workflowBuilderVm.ConnectionToNode = firstDecision;
            tests.Check(!workflowBuilderVm.CanCreateLink && !workflowBuilderVm.AddLink(), "A connection should require distinct From and To nodes.");

            workflowBuilderVm.ConnectionToNode = secondDecision;
            tests.Check(workflowBuilderVm.CanCreateLink, "A connection should become available after distinct From and To nodes are selected.");
            tests.Check(workflowBuilderVm.AddLink(), "Workflow builder should create a connection with explicit distinct endpoints.");
            var explicitLink = workflowBuilderVm.SelectedWorkflow.Links.LastOrDefault();
            tests.Check(explicitLink is not null &&
                        explicitLink.FromNodeId == firstDecision.Id &&
                        explicitLink.ToNodeId == secondDecision.Id &&
                        explicitLink.Label == "Decision route",
                "A created connection should preserve the exact selected endpoints and label.");
            var connectionCountAfterExplicitLink = workflowBuilderVm.SelectedWorkflow.Links.Count;

            workflowBuilderVm.ConnectionFromNode = firstDecision;
            workflowBuilderVm.ConnectionToNode = secondDecision;
            workflowBuilderVm.ConnectionLabel = "Duplicate route";
            tests.Check(!workflowBuilderVm.CanCreateLink, "An existing pair of connection endpoints should not be offered again.");
            tests.Check(!workflowBuilderVm.AddLink() && workflowBuilderVm.SelectedWorkflow.Links.Count == connectionCountAfterExplicitLink,
                "Workflow builder should prevent duplicate connections between the same endpoints.");

            var firstAddedNodeId = duplicatedDecision.Id;
            workflowBuilderVm.SelectedNode = duplicatedDecision;
            tests.Check(workflowBuilderVm.RemoveSelectedNode(), "Workflow builder should remove the selected node during ID regression setup.");
            workflowBuilderVm.AddNode(WorkflowNodeKind.Decision);
            tests.Check(workflowBuilderVm.SelectedNode!.Id != firstAddedNodeId, "Deleting and adding a workflow node should never reuse an earlier node ID.");
            tests.Check(workflowBuilderVm.SelectedWorkflow!.Nodes.Select(node => node.Id).Distinct(StringComparer.Ordinal).Count() == workflowBuilderVm.SelectedWorkflow.Nodes.Count, "Workflow builder node IDs should remain unique after delete and add operations.");
            workflowBuilderVm.SelectedNode!.X = 1260;
            workflowBuilderVm.SelectedNode.Width = 220;
            tests.Check(workflowBuilderVm.DiagramCanvasWidth >= 1576, "Workflow builder canvas should expand to include far-right nodes.");
            workflowBuilderVm.SelectedNode.Y = 780;
            workflowBuilderVm.SelectedNode.Height = 120;
            tests.Check(workflowBuilderVm.DiagramCanvasHeight >= 996, "Workflow builder canvas should expand to include lower nodes.");

            workflowDefinition.Id = "  workflow id with spaces  ";
            workflowDefinition.Category = "Test plans";
            workflowDefinition.Description = "Round-trip workflow description";
            tests.Check(workflowDefinition.Id == "workflow id with spaces", "Workflow IDs should trim outer whitespace without applying display-label cleanup.");
            workflowDefinition.Nodes.Add(new WorkflowDiagramNode
            {
                Id = " start ",
                Kind = WorkflowNodeKind.Start,
                Title = "Start",
                Description = "Round-trip node description",
                Color = WorkflowNodeColor.Rose,
                Goal = "Round-trip goal",
                Instructions = "Round-trip instructions",
                ExpectedOutput = "Round-trip output",
                X = 123.4,
                Y = 234.5,
                Width = 210.6,
                Height = 98.7,
                ChecklistItems = new ObservableCollection<WorkflowChecklistItem>
                {
                    new() { Text = "First check" },
                    new() { Text = "Done check", IsDone = true }
                }
            });
            workflowDefinition.Nodes.Add(new WorkflowDiagramNode { Id = "end", Kind = WorkflowNodeKind.End, Title = "End", Color = WorkflowNodeColor.Green });
            tests.Check(workflowDefinition.Nodes[0].Id == "start", "Workflow node IDs should use identity cleanup, not display-label cleanup.");
            workflowDefinition.Links.Add(new WorkflowDiagramLink { Id = "primary-link", FromNodeId = "start", ToNodeId = "end", Label = "Continue" });
            workflowService.Save(workflowDefinition);
            tests.Check(!string.IsNullOrWhiteSpace(workflowDefinition.FilePath), "Workflow save should assign a file path.");
            var savedWorkflowJson = File.ReadAllText(workflowDefinition.FilePath!);
            tests.Check(savedWorkflowJson.Contains("\n  \"SchemaVersion\":", StringComparison.Ordinal), "Saved workflow JSON should remain indented and readable in a text editor.");
            tests.Check(workflowService.TryLoad(workflowDefinition.FilePath!, out var loadedWorkflow), "Workflow service should reload saved workflow JSON.");
            tests.Check(loadedWorkflow.SchemaVersion == WorkflowDefinition.CurrentSchemaVersion, "Workflow service should normalize saved workflows to the current schema.");
            tests.Check(loadedWorkflow.Id == "workflow id with spaces" && loadedWorkflow.Name == "Colour test", "Workflow identity and name should persist through JSON save/load.");
            tests.Check(loadedWorkflow.Category == "Test plans" && loadedWorkflow.Description == "Round-trip workflow description", "Workflow category and description should persist through JSON save/load.");
            tests.Check(loadedWorkflow.Nodes.Count == 2, "Workflow node count should persist through JSON save/load.");
            var loadedStartNode = loadedWorkflow.Nodes.FirstOrDefault(node => node.Id == "start");
            tests.Check(loadedStartNode is not null, "Workflow node IDs should persist through JSON save/load.");
            if (loadedStartNode is not null)
            {
                tests.Check(loadedStartNode.Kind == WorkflowNodeKind.Start && loadedStartNode.Title == "Start", "Workflow node kind and title should persist through JSON save/load.");
                tests.Check(loadedStartNode.Description == "Round-trip node description", "Workflow node description should persist through JSON save/load.");
                tests.Check(loadedStartNode.Color == WorkflowNodeColor.Rose, "Workflow node colour should persist through JSON save/load.");
                tests.Check(loadedStartNode.Goal == "Round-trip goal", "Workflow node goal should persist through JSON save/load.");
                tests.Check(loadedStartNode.Instructions == "Round-trip instructions", "Workflow node instructions should persist through JSON save/load.");
                tests.Check(loadedStartNode.ExpectedOutput == "Round-trip output", "Workflow node expected output should persist through JSON save/load.");
                tests.Check(loadedStartNode.X == 123.4 && loadedStartNode.Y == 234.5, "Workflow node position should persist through JSON save/load.");
                tests.Check(loadedStartNode.Width == 210.6 && loadedStartNode.Height == 98.7, "Workflow node size should persist through JSON save/load.");
                tests.Check(loadedStartNode.ChecklistItems.Count == 2 &&
                            loadedStartNode.ChecklistItems[0].Text == "First check" &&
                            !loadedStartNode.ChecklistItems[0].IsDone &&
                            loadedStartNode.ChecklistItems[1].Text == "Done check" &&
                            loadedStartNode.ChecklistItems[1].IsDone,
                    "Workflow node checklist text and completion state should persist through JSON save/load.");
            }

            var loadedLink = loadedWorkflow.Links.FirstOrDefault(link => link.Id == "primary-link");
            tests.Check(loadedLink is not null &&
                        loadedLink.FromNodeId == "start" &&
                        loadedLink.ToNodeId == "end" &&
                        loadedLink.Label == "Continue",
                "Workflow link identity, endpoints, and label should persist through JSON save/load.");
            var readableWorkflowText = workflowService.BuildTextExport(workflowDefinition);
            tests.Check(readableWorkflowText.StartsWith(WorkflowService.TextExportMarker, StringComparison.Ordinal), "Workflow text export should include a clear ColumnPad marker.");
            tests.Check(readableWorkflowText.Contains("Readable copy only; import the .workflow.json file to continue editing.", StringComparison.Ordinal), "Workflow text export should explain that the readable copy is not reloadable.");
            tests.Check(readableWorkflowText.Contains("Workflow: Colour test"), "Workflow text export should include the workflow name.");
            tests.Check(readableWorkflowText.Contains("1. [Start] Start"), "Workflow text export should list readable node steps.");
            tests.Check(readableWorkflowText.Contains("Round-trip goal"), "Workflow text export should include node goals.");
            tests.Check(readableWorkflowText.Contains("- [x] Done check"), "Workflow text export should include checklist completion state.");
            tests.Check(readableWorkflowText.Contains("Next:\r\n  - 2. End (Continue)", StringComparison.Ordinal) ||
                        readableWorkflowText.Contains("Next:\n  - 2. End (Continue)", StringComparison.Ordinal),
                "Workflow text export should show each connection once as the next step.");
            tests.Check(!readableWorkflowText.Contains("Connections", StringComparison.Ordinal), "Workflow text export should not repeat connections in a second summary.");
            var readableWorkflowTextPath = Path.Combine(workflowTemp, "colour-test.workflow.txt");
            workflowService.ExportTextToPath(workflowDefinition, readableWorkflowTextPath);
            tests.Check(File.Exists(readableWorkflowTextPath), "Workflow text export should write a text file.");
            var existingWorkflowVm = new WorkflowBuilderViewModel(workflowService);
            existingWorkflowVm.Load();
            var workflowCountBeforeAdd = existingWorkflowVm.Workflows.Count;
            existingWorkflowVm.AddWorkflow();
            tests.Check(existingWorkflowVm.Workflows.Count == workflowCountBeforeAdd + 1, "Workflow Builder Add Workflow should add one workflow.");

            tests.Check(!WorkflowService.IsWorkflowDefinitionJson("{}"), "Workflow detection should reject unrelated empty JSON objects.");
            tests.Check(!WorkflowService.IsWorkflowDefinitionJson(layoutJson), "Workflow detection should reject ColumnPad layout JSON.");
            var camelCaseWorkflowPath = Path.Combine(workflowTemp, "camel-case.workflow.json");
            File.WriteAllText(camelCaseWorkflowPath, """
            {
              "fileType": "ColumnPadWorkflow",
              "schemaVersion": 3,
              "id": "camel-case",
              "name": "Camel Case Workflow",
              "nodes": [
                { "id": "start", "kind": "Start", "title": "Start" }
              ],
              "links": []
            }
            """);
            tests.Check(workflowService.TryLoad(camelCaseWorkflowPath, out var camelCaseWorkflow), "Workflow import should accept case-insensitive property names and readable enum names.");
            tests.Check(camelCaseWorkflow.Nodes.Count == 1 && camelCaseWorkflow.Nodes[0].Kind == WorkflowNodeKind.Start, "Case-insensitive workflow import should preserve node data.");

            var legacyWorkflowPath = Path.Combine(workflowTemp, "legacy.workflow.json");
            File.WriteAllText(legacyWorkflowPath, """
            {
              "SchemaVersion": 1,
              "Id": "legacy-flow",
              "Name": "Legacy Workflow",
              "Category": "Compatibility",
              "Description": "An older executable workflow.",
              "Trigger": "Manual",
              "Steps": [
                { "Kind": "SetColumnCount", "Argument": "4", "Notes": "Prepare four writing areas." },
                { "Kind": "SetTheme", "Argument": "Dark Mode", "Notes": "Use the dark palette." }
              ]
            }
            """);
            tests.Check(WorkflowService.IsWorkflowDefinitionJson(File.ReadAllText(legacyWorkflowPath)), "Workflow detection should recognize the published version-1 Steps format.");
            tests.Check(workflowService.TryLoad(legacyWorkflowPath, out var migratedLegacyWorkflow), "Workflow service should migrate version-1 Steps workflows.");
            tests.Check(migratedLegacyWorkflow.SchemaVersion == WorkflowDefinition.CurrentSchemaVersion, "Migrated workflows should use the current schema.");
            tests.Check(migratedLegacyWorkflow.Nodes.Count == 4 && migratedLegacyWorkflow.Links.Count == 3, "Legacy steps should become one connected Start-to-End diagram.");
            tests.Check(migratedLegacyWorkflow.Nodes[1].Title == "Set column count" && migratedLegacyWorkflow.Nodes[1].Instructions.Contains('4'), "Legacy step kind and argument data should remain readable after migration.");
            tests.Check(migratedLegacyWorkflow.Nodes[2].Description == "Use the dark palette.", "Legacy step notes should be preserved during migration.");

            var futureWorkflowJson = $$"""
            {
              "FileType": "ColumnPadWorkflow",
              "SchemaVersion": {{WorkflowDefinition.CurrentSchemaVersion + 1}},
              "Nodes": [],
              "Links": []
            }
            """;
            tests.Check(!WorkflowService.IsWorkflowDefinitionJson(futureWorkflowJson), "Workflow detection should reject unsupported future schema versions.");

            var invalidWorkflowPath = Path.Combine(workflowTemp, "invalid.workflow.json");
            File.WriteAllText(invalidWorkflowPath, "{}");
            _ = workflowService.LoadAll();
            tests.Check(workflowService.LastLoadWarnings.Contains("invalid.workflow.json"), "Workflow library loading should report unreadable workflow filenames instead of silently skipping them.");

            var dirtyWorkflowService = new WorkflowService(Path.Combine(workflowTemp, "dirty-state"));
            var dirtyWorkflowVm = new WorkflowBuilderViewModel(dirtyWorkflowService);
            dirtyWorkflowVm.Load();
            tests.Check(!dirtyWorkflowVm.HasUnsavedChanges, "Opening an empty Workflow Builder should not treat its untouched blank draft as a user edit.");
            dirtyWorkflowVm.SelectedWorkflow!.Name = "My Workflow";
            tests.Check(dirtyWorkflowVm.HasUnsavedChanges, "Editing the blank workflow draft should mark it unsaved.");
            dirtyWorkflowVm.SaveSelectedWorkflow();
            tests.Check(!dirtyWorkflowVm.HasUnsavedChanges, "Saving a workflow should establish a clean state.");
            dirtyWorkflowVm.SelectedWorkflow!.Description = "Changed after save";
            tests.Check(dirtyWorkflowVm.HasUnsavedChanges, "Editing workflow details should mark the Workflow Builder dirty.");
            tests.Check(dirtyWorkflowVm.SaveAllChangedWorkflows() == 1, "Save-all should save each changed workflow once.");
            tests.Check(!dirtyWorkflowVm.HasUnsavedChanges, "Save-all should clear the Workflow Builder dirty state.");
        }
        finally
        {
            if (Directory.Exists(workflowTemp))
                Directory.Delete(workflowTemp, recursive: true);
        }

        return workflowDefinition;
    }

    private static bool OverlapsAnyNode(WorkflowDiagramNode node, IEnumerable<WorkflowDiagramNode> nodes)
        => nodes.Any(other => !ReferenceEquals(node, other) && NodesOverlap(node, other));

    private static bool ContainsOverlappingNodes(IReadOnlyList<WorkflowDiagramNode> nodes)
    {
        for (var leftIndex = 0; leftIndex < nodes.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < nodes.Count; rightIndex++)
            {
                if (NodesOverlap(nodes[leftIndex], nodes[rightIndex]))
                    return true;
            }
        }

        return false;
    }

    private static bool NodesOverlap(WorkflowDiagramNode left, WorkflowDiagramNode right)
        => left.X < right.X + right.Width &&
           left.X + left.Width > right.X &&
           left.Y < right.Y + right.Height &&
           left.Y + left.Height > right.Y;
}
