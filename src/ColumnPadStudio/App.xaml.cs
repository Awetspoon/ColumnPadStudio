using ColumnPadStudio.Services;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColumnPadStudio;

public partial class App : Application
{
    private const long MaximumCrashLogBytes = 2 * 1024 * 1024;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        PreserveRecoveryForCrash();
        var logPath = WriteCrashLog(e.Exception);
        MessageBox.Show(
            "ColumnPad hit an unexpected error and needs to close.\n\nCrash details were saved here:\n" + logPath,
            "ColumnPad Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        Current.Shutdown(1);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        PreserveRecoveryForCrash();
        if (e.ExceptionObject is Exception exception)
        {
            WriteCrashLog(exception);
            return;
        }

        WriteCrashLog(new InvalidOperationException(e.ExceptionObject?.ToString() ?? "Unknown fatal error."));
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception);
        e.SetObserved();
    }

    private static void PreserveRecoveryForCrash()
    {
        if (Current?.MainWindow is MainWindow mainWindow)
            mainWindow.PreserveRecoveryForAbnormalShutdown();
    }

    private static string WriteCrashLog(Exception exception)
    {
        var detailsBuilder = new StringBuilder();
        detailsBuilder.AppendLine(CultureInfo.InvariantCulture, $"Timestamp: {DateTimeOffset.Now:O}");
        detailsBuilder.AppendLine(CultureInfo.InvariantCulture, $"App Version: {typeof(App).Assembly.GetName().Version}");
        detailsBuilder.AppendLine();
        detailsBuilder.AppendLine(exception.ToString());
        detailsBuilder.AppendLine(new string('-', 80));
        var details = detailsBuilder.ToString();

        Exception? lastWriteError = null;
        foreach (var logPath in GetCrashLogCandidates())
        {
            try
            {
                var directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                RotateCrashLogIfNeeded(logPath, Encoding.UTF8.GetByteCount(details));
                File.AppendAllText(logPath, details, Encoding.UTF8);
                return logPath;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastWriteError = ex;
            }
        }

        return "Unable to write crash log: " + lastWriteError?.Message;
    }

    private static IEnumerable<string> GetCrashLogCandidates()
    {
        yield return Path.Combine(AppStoragePaths.CrashLogsDirectory, "crash.log");
        yield return Path.Combine(Path.GetTempPath(), "ColumnPadStudio", "crash.log");
    }

    private static void RotateCrashLogIfNeeded(string logPath, int pendingBytes)
    {
        if (!File.Exists(logPath) || new FileInfo(logPath).Length + pendingBytes <= MaximumCrashLogBytes)
            return;

        var previousLogPath = Path.Combine(
            Path.GetDirectoryName(logPath) ?? Path.GetTempPath(),
            $"{Path.GetFileNameWithoutExtension(logPath)}.previous{Path.GetExtension(logPath)}");
        File.Move(logPath, previousLogPath, overwrite: true);
    }
}
