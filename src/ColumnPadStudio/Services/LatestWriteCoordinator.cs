using System.Diagnostics.CodeAnalysis;

namespace ColumnPadStudio.Services;

/// <summary>
/// Runs one background write at a time and keeps only the newest queued value.
/// </summary>
internal sealed class LatestWriteCoordinator<T> : IDisposable
{
    private readonly object _gate = new();
    private readonly Func<T, CancellationToken, Task> _writeAsync;
    private readonly Action<Exception?> _writeCompleted;

    private CancellationTokenSource _writeCancellation = new();
    private Task _workerTask = Task.CompletedTask;
    private T? _pendingValue;
    private bool _hasPendingValue;
    private bool _workerActive;
    private bool _acceptingWrites = true;

    public LatestWriteCoordinator(
        Func<T, CancellationToken, Task> writeAsync,
        Action<Exception?> writeCompleted)
    {
        _writeAsync = writeAsync ?? throw new ArgumentNullException(nameof(writeAsync));
        _writeCompleted = writeCompleted ?? throw new ArgumentNullException(nameof(writeCompleted));
    }

    public void Queue(T value)
    {
        lock (_gate)
        {
            if (!_acceptingWrites)
                return;

            _pendingValue = value;
            _hasPendingValue = true;
            if (!_workerActive)
                StartWorkerLocked();
        }
    }

    public async Task PauseAsync()
    {
        Task workerTask;
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            _acceptingWrites = false;
            _pendingValue = default;
            _hasPendingValue = false;
            workerTask = _workerTask;
            cancellation = _writeCancellation;
        }

        cancellation.Cancel();
        await workerTask.ConfigureAwait(false);
    }

    public void Resume()
    {
        CancellationTokenSource previousCancellation;
        lock (_gate)
        {
            if (_workerActive)
                throw new InvalidOperationException("The recovery writer must finish pausing before it can resume.");

            previousCancellation = _writeCancellation;
            _writeCancellation = new CancellationTokenSource();
            _acceptingWrites = true;
        }

        previousCancellation.Dispose();
    }

    public void StopAcceptingWithoutCancellation()
    {
        lock (_gate)
        {
            _acceptingWrites = false;
            _pendingValue = default;
            _hasPendingValue = false;
        }
    }

    public async Task WaitForIdleAsync()
    {
        while (true)
        {
            Task workerTask;
            lock (_gate)
            {
                if (!_workerActive && !_hasPendingValue)
                    return;

                workerTask = _workerTask;
            }

            await workerTask.ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        CancellationTokenSource cancellation;
        lock (_gate)
        {
            if (_workerActive)
                throw new InvalidOperationException("The recovery writer must be idle before it can be disposed.");

            _acceptingWrites = false;
            _pendingValue = default;
            _hasPendingValue = false;
            cancellation = _writeCancellation;
        }

        cancellation.Dispose();
    }

    private void StartWorkerLocked()
    {
        _workerActive = true;
        var cancellationToken = _writeCancellation.Token;
        _workerTask = Task.Run(() => RunWorkerAsync(cancellationToken));
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "This is the observed boundary for a fire-and-forget background writer; failures are reported to the UI.")]
    private async Task RunWorkerAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                T value;
                lock (_gate)
                {
                    if (cancellationToken.IsCancellationRequested || !_hasPendingValue)
                        return;

                    value = _pendingValue!;
                    _pendingValue = default;
                    _hasPendingValue = false;
                }

                try
                {
                    await _writeAsync(value, cancellationToken).ConfigureAwait(false);
                    if (!cancellationToken.IsCancellationRequested)
                        NotifyWriteCompleted(null);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    NotifyWriteCompleted(ex);
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _workerActive = false;
                if (_acceptingWrites && _hasPendingValue && !cancellationToken.IsCancellationRequested)
                    StartWorkerLocked();
            }
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A result callback must not fault the observed background-writer boundary.")]
    private void NotifyWriteCompleted(Exception? error)
    {
        try
        {
            _writeCompleted(error);
        }
        catch (Exception)
        {
            // The writer remains usable even if its optional status reporter is shutting down.
        }
    }
}
