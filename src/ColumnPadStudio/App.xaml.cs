using ColumnPadStudio.Services;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColumnPadStudio;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
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

    private static string WriteCrashLog(Exception exception)
    {
        var details = new StringBuilder()
            .AppendLine($"Timestamp: {DateTimeOffset.Now:O}")
            .AppendLine($"App Version: {typeof(App).Assembly.GetName().Version}")
            .AppendLine()
            .AppendLine(exception.ToString())
            .AppendLine(new string('-', 80))
            .ToString();

        Exception? lastWriteError = null;
        foreach (var logPath in GetCrashLogCandidates())
        {
            try
            {
                var directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.AppendAllText(logPath, details);
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
}
