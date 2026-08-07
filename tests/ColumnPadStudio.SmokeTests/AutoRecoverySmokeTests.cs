using ColumnPadStudio.Models;
using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json.Nodes;

namespace ColumnPadStudio.SmokeTests;

internal static class AutoRecoverySmokeTests
{
    private static readonly TimeSpan AsyncTestTimeout = TimeSpan.FromSeconds(5);

    public static async Task RunAsync(SmokeTestContext tests)
    {
        await CheckSnapshotSerializationAsync(tests);
        await CheckLatestWriteCoalescingAsync(tests);
        await CheckCleanCloseCancellationAsync(tests);
        await CheckCancelledCloseResumeAsync(tests);
        CheckRecoveryStoreCancellation(tests);
    }

    private static async Task CheckSnapshotSerializationAsync(SmokeTestContext tests)
    {
        var vm = new MainViewModel();
        vm.Columns[0].Title = "Captured column";
        vm.Columns[0].Text = "captured text";

        var directJson = vm.ToLayoutJson();
        var capturedSnapshot = vm.CaptureRecoveryLayoutSnapshot();
        var backgroundJson = await Task.Run(() => MainViewModel.SerializeLayoutSnapshot(capturedSnapshot));
        tests.Check(
            string.Equals(directJson, backgroundJson, StringComparison.Ordinal),
            "A UI-captured recovery layout should serialize identically on a background thread.");

        vm.Columns[0].Title = "Changed later";
        vm.Columns[0].Text = "changed text";
        var serializedCapture = await Task.Run(() => MainViewModel.SerializeLayoutSnapshot(capturedSnapshot));
        var capturedColumn = JsonNode.Parse(serializedCapture)?["Columns"]?[0]?.AsObject();
        tests.Check(
            capturedColumn?["Title"]?.GetValue<string>() == "Captured column" &&
            capturedColumn["Text"]?.GetValue<string>() == "captured text",
            "A captured recovery layout should remain detached from later UI model changes.");
    }

    private static async Task CheckLatestWriteCoalescingAsync(SmokeTestContext tests)
    {
        var writes = new ConcurrentQueue<int>();
        var results = new ConcurrentQueue<Exception?>();
        var firstWriteStarted = NewSignal();
        var releaseFirstWrite = NewSignal();
        var concurrentWrites = 0;
        var maximumConcurrentWrites = 0;

        using var writer = new LatestWriteCoordinator<int>(
            async (value, cancellationToken) =>
            {
                var currentWrites = Interlocked.Increment(ref concurrentWrites);
                UpdateMaximum(ref maximumConcurrentWrites, currentWrites);
                writes.Enqueue(value);
                try
                {
                    if (value == 1)
                    {
                        firstWriteStarted.TrySetResult(true);
                        await releaseFirstWrite.Task.WaitAsync(cancellationToken);
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref concurrentWrites);
                }
            },
            results.Enqueue);

        writer.Queue(1);
        await firstWriteStarted.Task.WaitAsync(AsyncTestTimeout);
        writer.Queue(2);
        writer.Queue(3);
        releaseFirstWrite.TrySetResult(true);
        await writer.WaitForIdleAsync().WaitAsync(AsyncTestTimeout);

        tests.Check(
            writes.SequenceEqual([1, 3]),
            "Auto-recovery should finish the active write and coalesce queued ticks to the newest snapshot.");
        tests.Check(
            maximumConcurrentWrites == 1,
            "Auto-recovery should never run more than one recovery write at a time.");
        tests.Check(
            results.Count == 2 && results.All(result => result is null),
            "Successful coalesced recovery writes should report observed completion.");
    }

    private static async Task CheckCleanCloseCancellationAsync(SmokeTestContext tests)
    {
        var writeStarted = NewSignal();
        var closeEvents = new ConcurrentQueue<string>();

        using var writer = new LatestWriteCoordinator<int>(
            async (_, cancellationToken) =>
            {
                closeEvents.Enqueue("write-started");
                writeStarted.TrySetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                closeEvents.Enqueue("published");
            },
            _ => { });

        writer.Queue(1);
        await writeStarted.Task.WaitAsync(AsyncTestTimeout);
        await writer.PauseAsync().WaitAsync(AsyncTestTimeout);

        closeEvents.Enqueue("cleared");
        await Task.Delay(25);
        tests.Check(
            closeEvents.SequenceEqual(["write-started", "cleared"]),
            "A clean close should cancel and drain an already-started save before recovery is cleared.");
    }

    private static async Task CheckCancelledCloseResumeAsync(SmokeTestContext tests)
    {
        var tokens = new ConcurrentQueue<CancellationToken>();
        var writes = new ConcurrentQueue<int>();
        var firstWriteStarted = NewSignal();

        using var writer = new LatestWriteCoordinator<int>(
            async (value, cancellationToken) =>
            {
                tokens.Enqueue(cancellationToken);
                if (value == 1)
                {
                    firstWriteStarted.TrySetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return;
                }

                writes.Enqueue(value);
            },
            _ => { });

        writer.Queue(1);
        await firstWriteStarted.Task.WaitAsync(AsyncTestTimeout);
        await writer.PauseAsync().WaitAsync(AsyncTestTimeout);
        writer.Resume();
        writer.Queue(2);
        await writer.WaitForIdleAsync().WaitAsync(AsyncTestTimeout);

        var observedTokens = tokens.ToArray();
        tests.Check(
            observedTokens.Length == 2 &&
            observedTokens[0].IsCancellationRequested &&
            !observedTokens[1].IsCancellationRequested &&
            writes.SequenceEqual([2]),
            "Cancelling a close prompt should resume auto-recovery with a fresh usable cancellation token.");
    }

    private static void CheckRecoveryStoreCancellation(SmokeTestContext tests)
    {
        var recoveryRoot = Path.Combine(Path.GetTempPath(), $"columnpad-recovery-cancel-{Guid.NewGuid():N}");
        var failedActivationRoot = Path.Combine(Path.GetTempPath(), $"columnpad-recovery-activation-{Guid.NewGuid():N}");
        try
        {
            var originalWorkspace = new WorkspaceRecoveryWorkspace(
                "Original",
                "original-layout",
                null,
                SaveFileKind.TextDocument,
                IsDirty: true,
                RequiresSaveAsBeforeOverwrite: false);
            WorkspaceRecoveryStore.Save([originalWorkspace], 0, recoveryRoot);

            var pointerPath = Path.Combine(recoveryRoot, "current-generation.txt");
            var originalGeneration = File.ReadAllText(pointerPath);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var cancellationObserved = false;
            try
            {
                WorkspaceRecoveryStore.Save(
                    [originalWorkspace with { Name = "Cancelled", LayoutJson = "cancelled-layout" }],
                    0,
                    recoveryRoot,
                    cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancellationObserved = true;
            }

            tests.Check(
                cancellationObserved && File.ReadAllText(pointerPath) == originalGeneration &&
                WorkspaceRecoveryStore.TryLoad(out var preservedSnapshot, recoveryRoot) &&
                preservedSnapshot.Workspaces[0].LayoutJson == "original-layout",
                "A cancelled recovery generation should leave the prior pointer and snapshot loadable.");

            var generationDirectory = Path.Combine(recoveryRoot, originalGeneration.Trim());
            var manifestPath = Path.Combine(generationDirectory, "manifest.json");
            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
            manifest["Workspaces"]![0]!["CurrentFileKind"] = "999";
            File.WriteAllText(manifestPath, manifest.ToJsonString());
            tests.Check(
                WorkspaceRecoveryStore.TryLoad(out var normalizedSnapshot, recoveryRoot) &&
                normalizedSnapshot.Workspaces[0].CurrentFileKind == SaveFileKind.Layout,
                "Recovery loading should normalize undefined numeric file kinds to a safe layout default.");

            Directory.CreateDirectory(Path.Combine(failedActivationRoot, "current-generation.txt"));
            var activationFailed = false;
            try
            {
                WorkspaceRecoveryStore.Save([originalWorkspace], 0, failedActivationRoot);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                activationFailed = true;
            }

            tests.Check(
                activationFailed &&
                Directory.GetDirectories(failedActivationRoot, "generation-*").Length == 0 &&
                !WorkspaceRecoveryStore.TryLoad(out _, failedActivationRoot),
                "A generation that fails before pointer activation should be removed instead of becoming fallback recovery.");
        }
        finally
        {
            if (Directory.Exists(recoveryRoot))
                Directory.Delete(recoveryRoot, recursive: true);
            if (Directory.Exists(failedActivationRoot))
                Directory.Delete(failedActivationRoot, recursive: true);
        }
    }

    private static TaskCompletionSource<bool> NewSignal()
    {
        return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        var current = Volatile.Read(ref maximum);
        while (candidate > current)
        {
            var observed = Interlocked.CompareExchange(ref maximum, candidate, current);
            if (observed == current)
                return;

            current = observed;
        }
    }
}
