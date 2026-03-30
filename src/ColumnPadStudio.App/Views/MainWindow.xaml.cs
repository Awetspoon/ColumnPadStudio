using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ColumnPadStudio.App.ViewModels;

namespace ColumnPadStudio.App.Views;

public partial class MainWindow : Window
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 0x00000002;

    public ShellViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
        RegisterInputBindings();
        Loaded += async (_, _) => await ViewModel.TryRestoreRecoveryAsync();
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    private void RegisterInputBindings()
    {
        InputBindings.Add(new KeyBinding(ViewModel.NewSessionCommand, new KeyGesture(Key.N, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ViewModel.AddWorkspaceCommand, new KeyGesture(Key.N, ModifierKeys.Control | ModifierKeys.Shift)));
        InputBindings.Add(new KeyBinding(ViewModel.CloseWorkspaceCommand, new KeyGesture(Key.W, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ViewModel.OpenCommand, new KeyGesture(Key.O, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ViewModel.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ViewModel.SaveAsCommand, new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift)));
        InputBindings.Add(new KeyBinding(ViewModel.FindCommand, new KeyGesture(Key.F, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ViewModel.FindNextCommand, new KeyGesture(Key.F3)));
        InputBindings.Add(new KeyBinding(ViewModel.ReplaceAllCommand, new KeyGesture(Key.H, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ViewModel.OpenSettingsCommand, new KeyGesture(Key.OemComma, ModifierKeys.Control)));
        InputBindings.Add(new KeyBinding(ViewModel.OpenWorkflowBuilderCommand, new KeyGesture(Key.W, ModifierKeys.Control | ModifierKeys.Alt)));
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WindowProc);
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmGetMinMaxInfo)
        {
            ApplyMonitorWorkAreaBounds(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void ApplyMonitorWorkAreaBounds(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo();
        if (!GetMonitorInfo(monitor, monitorInfo))
        {
            return;
        }

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var workArea = monitorInfo.WorkArea;
        var monitorArea = monitorInfo.MonitorArea;

        minMaxInfo.MaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
        minMaxInfo.MaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
        minMaxInfo.MaxSize.X = Math.Abs(workArea.Right - workArea.Left);
        minMaxInfo.MaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);

        Marshal.StructureToPtr(minMaxInfo, lParam, true);
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;

        if (!await ViewModel.ConfirmCloseApplicationAsync())
        {
            return;
        }

        Closing -= OnClosing;
        Close();
    }

    private void WorkspaceTab_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TabItem { DataContext: WorkspaceViewModel workspace })
        {
            ViewModel.SelectedWorkspace = workspace;
        }
    }

    private void WorkspaceTab_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not TabItem { DataContext: WorkspaceViewModel workspace } tabItem)
        {
            return;
        }

        ViewModel.SelectedWorkspace = workspace;

        var renameItem = new MenuItem
        {
            Header = "Rename Workspace",
            DataContext = workspace
        };
        renameItem.Click += WorkspaceTabRename_OnClick;

        tabItem.ContextMenu = new ContextMenu();
        tabItem.ContextMenu.Items.Add(renameItem);
    }

    private void WorkspaceTabRename_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: WorkspaceViewModel workspace })
        {
            return;
        }

        ViewModel.SelectedWorkspace = workspace;

        var window = new WorkspaceRenameWindow(workspace.Name)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            ViewModel.RenameWorkspace(workspace, window.WorkspaceName);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MonitorInfo
    {
        public int Size = Marshal.SizeOf<MonitorInfo>();
        public Rect MonitorArea;
        public Rect WorkArea;
        public int Flags;
    }
}
