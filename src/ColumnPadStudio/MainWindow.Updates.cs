using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using ColumnPadStudio.Services;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private readonly GitHubReleaseUpdateService _releaseUpdateService = new();
    private CancellationTokenSource? _releaseUpdateCancellation;
    private GitHubReleaseInfo? _availableRelease;

    private void InitializeUpdateNotification()
    {
        Loaded += MainWindow_UpdateCheckLoaded;
        Closed += MainWindow_UpdateCheckClosed;
    }

    private async void MainWindow_UpdateCheckLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_UpdateCheckLoaded;

        var cancellation = new CancellationTokenSource();
        _releaseUpdateCancellation = cancellation;

        try
        {
            var latestRelease = await _releaseUpdateService.GetLatestReleaseAsync(cancellation.Token);
            if (latestRelease is null || cancellation.IsCancellationRequested)
                return;

            var currentVersion = typeof(MainWindow).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
            if (!GitHubReleaseUpdateService.IsNewerRelease(latestRelease.Version, currentVersion))
                return;

            _availableRelease = latestRelease;
            UpdateAvailableButton.Content = $"Update {latestRelease.DisplayVersion}";
            UpdateAvailableButton.ToolTip = $"Open the ColumnPadStudio {latestRelease.DisplayVersion} release on GitHub";
            UpdateAvailableButton.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            // Closing the app or a short network timeout must not interrupt writing.
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            // Update checks are best-effort. Offline use remains fully supported.
        }
        finally
        {
            if (ReferenceEquals(_releaseUpdateCancellation, cancellation))
                _releaseUpdateCancellation = null;

            cancellation.Dispose();
        }
    }

    private void MainWindow_UpdateCheckClosed(object? sender, EventArgs e)
    {
        _releaseUpdateCancellation?.Cancel();
    }

    private void UpdateAvailable_Click(object sender, RoutedEventArgs e)
    {
        if (_availableRelease is null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(_availableRelease.ReleasePage.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            ActiveVm.StatusText = "Could not open the GitHub release page.";
        }
    }
}
