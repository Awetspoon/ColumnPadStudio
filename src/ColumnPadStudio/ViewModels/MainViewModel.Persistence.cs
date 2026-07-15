using System.IO;
using System.Text;
using ColumnPadStudio.Models;
using ColumnPadStudio.Services;

namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    public bool SaveCurrentFile()
    {
        if (!CanSaveCurrentFileDirectly)
            return false;

        SaveToPath(CurrentFilePath!, CurrentFileKind);
        return true;
    }

    public void SaveToPath(string path, SaveFileKind kind)
    {
        switch (kind)
        {
            case SaveFileKind.TextDocument:
            case SaveFileKind.MarkdownDocument:
                AtomicFileWriter.WriteText(path, BuildSingleDocumentText(), Encoding.UTF8);
                break;
            case SaveFileKind.TextExport:
                AtomicFileWriter.WriteText(path, BuildExportText(), Encoding.UTF8);
                break;
            case SaveFileKind.MarkdownExport:
                AtomicFileWriter.WriteText(path, BuildExportMarkdown(), Encoding.UTF8);
                break;
            default:
                AtomicFileWriter.WriteText(path, ToLayoutJson(), Encoding.UTF8);
                break;
        }

        SetCurrentFileReference(path, kind);
        StatusText = $"Saved: {Path.GetFileName(path)}";
        MarkClean();
    }

    public void NewLayout()
    {
        Columns.Clear();
        Columns.Add(MakeColumn("Column 1"));
        Columns.Add(MakeColumn("Column 2"));
        Columns.Add(MakeColumn("Column 3"));

        ActiveColumnId = Columns.First().Id;
        SetCurrentFileReference(null, SaveFileKind.Layout);
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        StatusText = "New layout.";
        MarkClean();
    }
}
