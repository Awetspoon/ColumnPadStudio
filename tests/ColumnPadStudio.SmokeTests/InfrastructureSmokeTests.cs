using ColumnPadStudio.Models;
using ColumnPadStudio.Services;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;

namespace ColumnPadStudio.SmokeTests;

internal static class InfrastructureSmokeTests
{
    public static async Task RunAsync(SmokeTestContext tests)
    {
        var preferencesPath = Path.Combine(Path.GetTempPath(), $"columnpad-preferences-{Guid.NewGuid():N}.json");
        try
        {
            AppPreferencesService.Save(
                new AppPreferences(
                    "Dark Mode",
                    SnapAllColumnsEnabled: false,
                    ColumnSpacingPx: 18,
                    FitColumnsToWindow: false,
                    DefaultColumnWidthPx: 444),
                preferencesPath);
            var loadedPreferences = AppPreferencesService.Load(preferencesPath);
            tests.Check(loadedPreferences.ThemePreset == "Dark Mode", "Saved app preferences should round-trip the selected theme.");
            tests.Check(!loadedPreferences.SnapAllColumnsEnabled, "Saved app preferences should round-trip the global unsnap-all choice.");
            tests.Check(loadedPreferences.ColumnSpacingPx == 18, "Saved app preferences should round-trip the column gap.");
            tests.Check(!loadedPreferences.FitColumnsToWindow, "Saved app preferences should keep Fit to window independent from snapping.");
            tests.Check(loadedPreferences.DefaultColumnWidthPx == 444, "Saved app preferences should round-trip a custom default column width.");
            AppPreferencesService.Save(loadedPreferences with { FitColumnsToWindow = true }, preferencesPath);
            var fittedPreferences = AppPreferencesService.Load(preferencesPath);
            tests.Check(fittedPreferences.FitColumnsToWindow, "Saved app preferences should round-trip the explicit Fit-to-window choice.");
            tests.Check(!fittedPreferences.SnapAllColumnsEnabled, "Changing Fit should not change the independent snapping choice.");
            AppPreferencesService.Save(
                new AppPreferences(ColumnSpacingPx: 999, DefaultColumnWidthPx: 99999),
                preferencesPath);
            tests.Check(
                AppPreferencesService.Load(preferencesPath).ColumnSpacingPx == AppPreferences.MaximumColumnSpacingPx,
                "Saved app preferences should clamp an excessive column gap.");
            tests.Check(
                AppPreferencesService.Load(preferencesPath).DefaultColumnWidthPx == (int)ColumnPadStudio.Domain.Workspaces.WorkspaceConstraints.MaximumColumnWidth,
                "Saved app preferences should clamp an excessive default column width.");
            AppPreferencesService.Save(new AppPreferences(DefaultColumnWidthPx: -1), preferencesPath);
            tests.Check(
                AppPreferencesService.Load(preferencesPath).DefaultColumnWidthPx == (int)ColumnPadStudio.Domain.Workspaces.WorkspaceConstraints.MinimumColumnWidth,
                "Saved app preferences should clamp a default column width below the safe minimum.");
            File.WriteAllText(preferencesPath, "{\"ThemePreset\":\"Light Mode\",\"SnapAllColumnsEnabled\":false,\"ColumnSpacingPx\":10}");
            var legacyPreferences = AppPreferencesService.Load(preferencesPath);
            tests.Check(legacyPreferences.ThemePreset == "Light Mode", "Older app preferences should preserve their saved theme.");
            tests.Check(!legacyPreferences.SnapAllColumnsEnabled, "Older app preferences should preserve their global snapping choice.");
            tests.Check(!legacyPreferences.FitColumnsToWindow, "Older app preferences should default to fixed widths now that Fit is independent from snapping.");
            tests.Check(
                legacyPreferences.DefaultColumnWidthPx == AppPreferences.StandardColumnWidthPx,
                "Older app preferences should migrate to the standard default column width.");
            tests.Check(
                legacyPreferences.ColumnSpacingPx == 10,
                "Older app preferences should preserve the saved column gap.");
            File.WriteAllText(preferencesPath, "{\"ThemePreset\":\"Light Mode\",\"SnapColumnsEnabled\":false,\"ColumnSpacingPx\":10}");
            var retiredPreferences = AppPreferencesService.Load(preferencesPath);
            tests.Check(retiredPreferences.SnapAllColumnsEnabled, "The current global snap setting should default on when only the retired field exists.");
            tests.Check(!retiredPreferences.FitColumnsToWindow, "A file predating global snap should use the new fixed-width default.");
            tests.Check(
                retiredPreferences.DefaultColumnWidthPx == AppPreferences.StandardColumnWidthPx,
                "A file predating default widths should use the standard 320px width.");
            File.WriteAllText(preferencesPath, "{\"ThemePreset\":\"Dark Mode\"}");
            var oldestPreferences = AppPreferencesService.Load(preferencesPath);
            tests.Check(oldestPreferences.SnapAllColumnsEnabled, "The oldest preference format should migrate to snapping on.");
            tests.Check(
                oldestPreferences.ColumnSpacingPx == AppPreferences.DefaultColumnSpacingPx,
                "The oldest preference format should migrate to the standard column gap.");
            tests.Check(!oldestPreferences.FitColumnsToWindow, "The oldest preference format should migrate to fixed column widths.");
            tests.Check(
                oldestPreferences.DefaultColumnWidthPx == AppPreferences.StandardColumnWidthPx,
                "The oldest preference format should migrate to the standard 320px width.");
            File.WriteAllText(preferencesPath, "{not valid json");
            var fallbackPreferences = AppPreferencesService.Load(out var preferencesWarning, preferencesPath);
            tests.Check(fallbackPreferences.ThemePreset == "Default Mode", "Invalid app preferences should fall back to the default theme.");
            tests.Check(fallbackPreferences.SnapAllColumnsEnabled, "Invalid app preferences should fall back to global snapping on.");
            tests.Check(!fallbackPreferences.FitColumnsToWindow, "Invalid app preferences should fall back to fixed column widths.");
            tests.Check(
                fallbackPreferences.DefaultColumnWidthPx == AppPreferences.StandardColumnWidthPx,
                "Invalid app preferences should fall back to the standard default column width.");
            tests.Check(
                fallbackPreferences.ColumnSpacingPx == AppPreferences.DefaultColumnSpacingPx,
                "Invalid app preferences should use the default column gap.");
            tests.Check(!string.IsNullOrWhiteSpace(preferencesWarning), "Invalid app preferences should report that defaults were used.");
            tests.Check(!File.Exists(preferencesPath), "Invalid app preferences should be moved out of the active settings path.");
            tests.Check(
                Directory.GetFiles(
                    Path.GetDirectoryName(preferencesPath)!,
                    Path.GetFileName(preferencesPath) + ".invalid-*").Length == 1,
                "Invalid app preferences should be retained as one recoverable backup.");
        }
        finally
        {
            if (File.Exists(preferencesPath))
                File.Delete(preferencesPath);

            foreach (var invalidPath in Directory.GetFiles(
                         Path.GetDirectoryName(preferencesPath)!,
                         Path.GetFileName(preferencesPath) + ".invalid-*"))
            {
                File.Delete(invalidPath);
            }
        }

        tests.Check(
            AppStoragePaths.CrashLogsDirectory == Path.Combine(AppStoragePaths.RootDirectory, "CrashLogs"),
            "App storage paths should expose the crash-log directory as a single source of truth.");
        tests.Check(
            typeof(MainWindow).Assembly.GetName().Name == "ColumnPadStudio",
            "The application assembly should publish with the stable ColumnPadStudio executable name.");

        const string latestReleaseJson = """
            {
              "tag_name": "v2.4.0",
              "html_url": "https://github.com/example-owner/ColumnPadStudio/releases/tag/v2.4.0"
            }
            """;
        using (var updateHttpClient = new HttpClient(new StaticJsonResponseHandler(latestReleaseJson)))
        {
            var updateService = new GitHubReleaseUpdateService(updateHttpClient);
            var latestRelease = await updateService.GetLatestReleaseAsync();

            tests.Check(latestRelease?.Version == new Version(2, 4, 0, 0), "GitHub update checks should parse release tags into comparable versions.");
            tests.Check(latestRelease?.DisplayVersion == "v2.4.0", "GitHub update checks should keep a clean version label for the notification.");
            tests.Check(latestRelease?.ReleasePage.AbsoluteUri == "https://github.com/example-owner/ColumnPadStudio/releases/tag/v2.4.0", "GitHub update checks should preserve the official HTTPS release page.");
            tests.Check(
                latestRelease is not null && GitHubReleaseUpdateService.IsNewerRelease(latestRelease.Version, new Version(2, 3, 0, 0)),
                "GitHub update checks should detect a newer stable release.");
            tests.Check(
                latestRelease is not null && !GitHubReleaseUpdateService.IsNewerRelease(latestRelease.Version, new Version(2, 4, 0, 0)),
                "GitHub update checks should not notify for the installed release.");
        }

        const string untrustedReleasePageJson = """
            {
              "tag_name": "v2.4.0",
              "html_url": "https://example.com/not-columnpad"
            }
            """;
        using (var updateHttpClient = new HttpClient(new StaticJsonResponseHandler(untrustedReleasePageJson)))
        {
            var updateService = new GitHubReleaseUpdateService(updateHttpClient);
            var latestRelease = await updateService.GetLatestReleaseAsync();
            tests.Check(
                latestRelease?.ReleasePage == GitHubReleaseUpdateService.ReleasesPageUri,
                "Update links should fall back to the trusted ColumnPadStudio GitHub releases page.");
        }

        using (var updateHttpClient = new HttpClient(
                   new StaticJsonResponseHandler("{}", System.Net.HttpStatusCode.NotFound)))
        {
            var updateService = new GitHubReleaseUpdateService(updateHttpClient);
            tests.Check(
                await updateService.GetLatestReleaseAsync() is null,
                "Update checks should quietly handle a repository with no published release.");
        }

        tests.Check(
            GitHubReleaseUpdateService.TryParseReleaseVersion("v2.5.0-beta.1", out var parsedReleaseVersion)
                && parsedReleaseVersion == new Version(2, 5, 0, 0),
            "Release version parsing should ignore semantic-version labels when comparing versions.");
        tests.Check(
            !GitHubReleaseUpdateService.TryParseReleaseVersion("latest", out _),
            "Release version parsing should reject tags that do not contain a numeric version.");

        var atomicRoot = Path.Combine(Path.GetTempPath(), $"columnpad-atomic-{Guid.NewGuid():N}");
        try
        {
            var atomicPath = Path.Combine(atomicRoot, "nested", "note.txt");
            AtomicFileWriter.WriteText(atomicPath, "first");
            tests.Check(File.ReadAllText(atomicPath) == "first", "Atomic writer should create missing target directories.");
            AtomicFileWriter.WriteText(atomicPath, "second");
            tests.Check(File.ReadAllText(atomicPath) == "second", "Atomic writer should replace existing files cleanly.");
            tests.Check(Directory.GetFiles(Path.GetDirectoryName(atomicPath)!, "*.tmp").Length == 0, "Atomic writer should clean up temporary files after a successful write.");
        }
        finally
        {
            if (Directory.Exists(atomicRoot))
                Directory.Delete(atomicRoot, recursive: true);
        }

        var maximumWorkspaceEntries = Enumerable
            .Range(1, WorkspaceSessionFileService.MaxWorkspaces)
            .Select(index => new WorkspaceSessionEntryData($"Workspace {index}", "{}", 3))
            .ToList();
        var maximumWorkspaceSessionJson = WorkspaceSessionFileService.SerializeSession(maximumWorkspaceEntries, 0);
        tests.Check(
            WorkspaceSessionFileService.TryParseSession(maximumWorkspaceSessionJson, out var maximumWorkspaceSession) &&
            maximumWorkspaceSession.Workspaces.Count == WorkspaceSessionFileService.MaxWorkspaces,
            "Workspace sessions should accept the shared maximum workspace count.");

        var oversizedWorkspaceEntries = maximumWorkspaceEntries
            .Append(new WorkspaceSessionEntryData("Workspace 65", "{}", 3))
            .ToList();
        var oversizedSessionSaveRejected = false;
        try
        {
            _ = WorkspaceSessionFileService.SerializeSession(oversizedWorkspaceEntries, 0);
        }
        catch (ArgumentException)
        {
            oversizedSessionSaveRejected = true;
        }

        tests.Check(
            oversizedSessionSaveRejected,
            "Workspace-session saves should reject more than the shared maximum workspace count.");

        var oversizedSessionRoot = JsonNode.Parse(maximumWorkspaceSessionJson)!.AsObject();
        oversizedSessionRoot["Workspaces"]!.AsArray().Add(new JsonObject
        {
            ["Name"] = "Workspace 65",
            ["Layout"] = new JsonObject(),
            ["LastMultiColumnCount"] = 3
        });
        var oversizedWorkspaceSessionJson = oversizedSessionRoot.ToJsonString();
        tests.Check(
            !WorkspaceSessionFileService.IsWorkspaceSessionJson(oversizedWorkspaceSessionJson) &&
            !WorkspaceSessionFileService.TryParseSession(oversizedWorkspaceSessionJson, out _),
            "Workspace-session detection and loading should reject more than the shared maximum workspace count.");

        var recoveryLimitRoot = Path.Combine(Path.GetTempPath(), $"columnpad-recovery-limit-{Guid.NewGuid():N}");
        try
        {
            var oversizedRecoveryWorkspaces = Enumerable
                .Range(1, WorkspaceSessionFileService.MaxWorkspaces + 1)
                .Select(index => new WorkspaceRecoveryWorkspace(
                    $"Workspace {index}",
                    "{}",
                    null,
                    SaveFileKind.Layout,
                    IsDirty: true,
                    RequiresSaveAsBeforeOverwrite: false))
                .ToList();
            var oversizedRecoverySaveRejected = false;
            try
            {
                WorkspaceRecoveryStore.Save(oversizedRecoveryWorkspaces, 0, recoveryLimitRoot);
            }
            catch (ArgumentException)
            {
                oversizedRecoverySaveRejected = true;
            }

            tests.Check(
                oversizedRecoverySaveRejected && !Directory.Exists(recoveryLimitRoot),
                "Oversized recovery snapshots should be rejected before recovery files are written.");

            Directory.CreateDirectory(recoveryLimitRoot);
            var recoveryWorkspaceNodes = new JsonArray();
            for (var index = 1; index <= WorkspaceSessionFileService.MaxWorkspaces + 1; index++)
                recoveryWorkspaceNodes.Add(new JsonObject { ["Name"] = $"Workspace {index}" });

            var oversizedRecoveryManifest = new JsonObject
            {
                ["Version"] = 1,
                ["SavedUtc"] = DateTime.UtcNow,
                ["ActiveWorkspaceIndex"] = 0,
                ["Workspaces"] = recoveryWorkspaceNodes
            };
            File.WriteAllText(
                Path.Combine(recoveryLimitRoot, "manifest.json"),
                oversizedRecoveryManifest.ToJsonString());
            tests.Check(
                !WorkspaceRecoveryStore.TryLoad(out _, recoveryLimitRoot),
                "Recovery loading should reject manifests beyond the shared maximum workspace count.");
        }
        finally
        {
            if (Directory.Exists(recoveryLimitRoot))
                Directory.Delete(recoveryLimitRoot, recursive: true);
        }

        await AutoRecoverySmokeTests.RunAsync(tests);
    }
}
