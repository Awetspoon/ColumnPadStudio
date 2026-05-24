using ColumnPadStudio.Controls;
using System.Windows;
using System.Windows.Input;

namespace ColumnPadStudio;

public partial class MainWindow
{
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPreviewKeyDown(e);

        if (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.None)
        {
            FindNext_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift))
        {
            if (e.Key == Key.Left)
            {
                MoveActiveLeft_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Right)
            {
                MoveActiveRight_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            var quickIndex = ToQuickJumpIndex(e.Key);
            if (quickIndex >= 0)
            {
                QuickJumpToColumn(quickIndex);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Z)
            {
                ActiveVm.WordWrap = !ActiveVm.WordWrap;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.L)
            {
                ActiveVm.ShowLineNumbers = !ActiveVm.ShowLineNumbers;
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (e.Key is Key.OemPlus or Key.Add)
            {
                AddColumn_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key is Key.OemMinus or Key.Subtract)
            {
                RemoveActive_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S)
            {
                SaveAs_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key is Key.D8 or Key.NumPad8)
            {
                ShowGutterBullets_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key is Key.D7 or Key.NumPad7)
            {
                ShowGutterChecklist_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key is Key.D1 or Key.NumPad1)
            {
                SingleTextMode_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
            if (e.Key is Key.D2 or Key.NumPad2)
            {
                ColumnMode_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.B)
            {
                OpenWorkflowBuilder_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.L)
            {
                LockActiveWidth_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N)
            {
                NewWorkspaceTab_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.W)
            {
                CloseWorkspaceTab_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.E)
            {
                ExportMarkdown_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.X)
            {
                ClearAll_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.Enter)
            {
                SelectionToggleChecks_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N)
            {
                NewLayout_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.O)
            {
                OpenLayout_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S)
            {
                Save_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.E)
            {
                ExportTxt_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.P)
            {
                Print_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F)
            {
                Find_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.H)
            {
                ReplaceAll_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.D)
            {
                DuplicateActive_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (e.Key == Key.R)
            {
                ResetWidths_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }
        }
    }

    private static int ToQuickJumpIndex(Key key)
    {
        return key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            Key.D7 or Key.NumPad7 => 6,
            Key.D8 or Key.NumPad8 => 7,
            Key.D9 or Key.NumPad9 => 8,
            _ => -1
        };
    }

    private void QuickJumpToColumn(int zeroBasedIndex)
    {
        var jumpTargets = ActiveVm.GetQuickJumpColumns();
        if (zeroBasedIndex < 0 || zeroBasedIndex >= jumpTargets.Count)
            return;

        var column = jumpTargets[zeroBasedIndex];
        ActiveVm.ActiveColumnId = column.Id;
        ActiveVm.StatusText = $"Jumped to {column.Title}.";

        if (_editorsById.TryGetValue(column.Id, out var editor))
            editor.FocusEditor();
    }

    private ColumnEditorControl? GetActiveEditorControl()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return null;

        _editorsById.TryGetValue(active.Id, out var editor);
        return editor;
    }
}
