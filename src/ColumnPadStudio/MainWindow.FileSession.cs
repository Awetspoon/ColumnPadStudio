using ColumnPadStudio.Models;
using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private void NewLayout_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmWorkspaceDestructiveAction(ActiveWorkspace, "New Layout", "Creating a new layout"))
            return;

        ActiveVm.NewLayout();
        ResetFindCursor();
    }

    private void OpenLayout_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = FileWorkflowService.SupportedOpenFileFilter,
            FilterIndex = 1
        };

        if (dlg.ShowDialog() != true)
            return;

        var extension = Path.GetExtension(dlg.FileName).ToLowerInvariant();
        var fileName = Path.GetFileName(dlg.FileName);
        var content = string.Empty;
        var loadKind = OpenFileLoadKind.LayoutJson;

        if (!TryRunFileAction("Open Failed", $"open {fileName}", () =>
        {
            content = File.ReadAllText(dlg.FileName);
            loadKind = FileWorkflowService.ClassifyOpenFile(extension, content);
        }))
        {
            return;
        }

        if (loadKind == OpenFileLoadKind.WorkflowJson)
        {
            OpenWorkflowBuilder(dlg.FileName);
            ActiveVm.StatusText = $"Opened workflow in builder: {fileName}";
            return;
        }

        if (!ConfirmWorkspaceDestructiveAction(ActiveWorkspace, "Open File", "Opening a file"))
            return;

        if (!TryRunFileAction("Open Failed", $"open {fileName}", () =>
        {
            switch (loadKind)
            {
                case OpenFileLoadKind.TextExport:
                    ActiveVm.LoadFromExportText(content, fileName, dlg.FileName);
                    break;
                case OpenFileLoadKind.TextDocument:
                    ActiveVm.LoadTextDocument(content, fileName, dlg.FileName, SaveFileKind.TextDocument);
                    break;
                case OpenFileLoadKind.MarkdownExport:
                    ActiveVm.LoadFromExportMarkdown(content, fileName, dlg.FileName);
                    break;
                case OpenFileLoadKind.MarkdownDocument:
                    ActiveVm.LoadTextDocument(content, fileName, dlg.FileName, SaveFileKind.MarkdownDocument);
                    break;
                case OpenFileLoadKind.WorkspaceSession:
                    if (!TryLoadWorkspaceSession(content, fileName, dlg.FileName))
                        ActiveVm.LoadFromJson(content, fileName, dlg.FileName, preserveCurrentTheme: true);
                    break;
                default:
                    ActiveVm.LoadFromJson(content, fileName, dlg.FileName, preserveCurrentTheme: true);
                    break;
            }

            ResetFindCursor();
        }))
        {
            return;
        }
    }


    private void Save_Click(object sender, RoutedEventArgs e)
    {
        PersistWidthsFromGrid();

        if (ShouldSaveWorkspaceSession())
        {
            var sessionPath = GetDirectWorkspaceSessionPath();
            if (string.IsNullOrWhiteSpace(sessionPath))
            {
                SaveAs_Click(sender, e);
                return;
            }

            TryRunFileAction("Save Failed", $"save {Path.GetFileName(sessionPath)}", () => SaveWorkspaceSessionToPath(sessionPath));
            return;
        }

        if (ActiveVm.CanSaveCurrentFileDirectly)
        {
            TryRunFileAction("Save Failed", $"save {Path.GetFileName(ActiveVm.CurrentFilePath)}", () => ActiveVm.SaveCurrentFile());
            return;
        }

        SaveAs_Click(sender, e);
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        PersistWidthsFromGrid();

        if (ShouldSaveWorkspaceSession())
        {
            var sessionDialog = CreateWorkspaceSessionSaveDialog();
            if (sessionDialog.ShowDialog() != true)
                return;

            TryRunFileAction("Save Failed", $"save {Path.GetFileName(sessionDialog.FileName)}", () => SaveWorkspaceSessionToPath(sessionDialog.FileName));
            return;
        }

        var dlg = CreateSaveDialog(ActiveVm);
        if (dlg.ShowDialog() != true)
            return;

        TryRunFileAction("Save Failed", $"save {Path.GetFileName(dlg.FileName)}", () => ActiveVm.SaveToPath(dlg.FileName, ActiveVm.CurrentFileKind));
    }

    private static SaveFileDialog CreateSaveDialog(MainViewModel vm)
    {
        var definition = FileWorkflowService.BuildSaveDialog(
            vm.CurrentFileKind,
            vm.CurrentFilePath,
            vm.RequiresSaveAsBeforeOverwrite);

        return CreateSaveFileDialog(definition);
    }

    private static SaveFileDialog CreateExportDialog(SaveFileKind kind)
    {
        var definition = FileWorkflowService.BuildSaveDialog(
            kind,
            currentFilePath: null,
            requiresSaveAsBeforeOverwrite: false);

        return CreateSaveFileDialog(definition);
    }

    private static SaveFileDialog CreateSaveFileDialog(FileDialogDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new SaveFileDialog
        {
            FileName = definition.FileName,
            Filter = definition.Filter,
            DefaultExt = definition.DefaultExt,
            AddExtension = definition.AddExtension
        };
    }

    private void ExportTxt_Click(object sender, RoutedEventArgs e)
    {
        var dlg = CreateExportDialog(SaveFileKind.TextExport);
        if (dlg.ShowDialog() != true)
            return;

        TryRunFileAction("Export Failed", $"export {Path.GetFileName(dlg.FileName)}", () =>
        {
            AtomicFileWriter.WriteText(dlg.FileName, ActiveVm.BuildExportText(), Encoding.UTF8);
            ActiveVm.StatusText = $"Exported: {Path.GetFileName(dlg.FileName)}";
        });
    }

    private void ExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        var dlg = CreateExportDialog(SaveFileKind.MarkdownExport);
        if (dlg.ShowDialog() != true)
            return;

        TryRunFileAction("Export Failed", $"export {Path.GetFileName(dlg.FileName)}", () =>
        {
            AtomicFileWriter.WriteText(dlg.FileName, ActiveVm.BuildExportMarkdown(), Encoding.UTF8);
            ActiveVm.StatusText = $"Exported: {Path.GetFileName(dlg.FileName)}";
        });
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true)
            return;

        var document = new FlowDocument
        {
            PagePadding = new Thickness(42),
            ColumnWidth = double.PositiveInfinity,
            FontFamily = new FontFamily(ActiveVm.EditorFontFamily),
            FontSize = ActiveVm.EditorFontSize
        };

        document.Blocks.Add(new Paragraph(new Run(ActiveVm.BuildExportText())));
        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        paginator.PageSize = new Size(dlg.PrintableAreaWidth, dlg.PrintableAreaHeight);
        dlg.PrintDocument(paginator, "ColumnPad print");
        ActiveVm.StatusText = "Sent to printer.";
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();
}
