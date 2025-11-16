namespace DataFlow.POC.Core;

using System.Threading.Channels;

/// <summary>
/// Provides serialized access to a service instance using a channel-backed pattern.
/// Multiple concurrent callers can submit operations via the channel writer,
/// and a single reader task processes them sequentially.
/// 
/// Operations are queued to the channel and the returned task completes when the operation
/// is successfully written to the channel, NOT when execution completes. This encourages
/// blocks to submit their work and continue without blocking on execution completion.
/// </summary>
/// <typeparam name="TService">The type of service to provide serialized access to.</typeparam>
internal sealed class SerializedServiceExecutor<TService> : IAsyncDisposable
    where TService : notnull
{
    private readonly TService _service;
    private readonly Channel<OperationRequest> _channel;
    private readonly Task _readerTask;
    private readonly CancellationTokenSource _shutdownCts;
    private int _queuedCount;
    private int _executedCount;
    private bool _disposed;

    /// <summary>
    /// Task that completes when all queued operations have been executed.
    /// </summary>
    public Task CompletionTask => _readerTask;

    public SerializedServiceExecutor(TService service, CancellationToken cancellationToken = default)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        
        // Unbounded channel: we don't want to block writers
        // Operations are fire-and-forget from caller's perspective
        _channel = Channel.CreateUnbounded<OperationRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        _shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // Start the reader task that processes operations sequentially
        _readerTask = Task.Run(() => ProcessOperationsAsync(_shutdownCts.Token), _shutdownCts.Token);
    }

    /// <summary>
    /// Queues an operation for serialized execution.
    /// Returns a task that completes when the operation is successfully queued,
    /// NOT when the operation has been executed.
    /// </summary>
    public Task QueueOperationAsync(
        Func<TService, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(operation);

        var request = new OperationRequest(operation, cancellationToken);

        // Write to channel (non-blocking since we use unbounded channel)
        if (!_channel.Writer.TryWrite(request))
        {
            throw new InvalidOperationException("Failed to queue operation - channel may be closed");
        }

        // Increment queued counter
        Interlocked.Increment(ref _queuedCount);

        // Return completed task - operation is queued but not yet executed
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the number of operations queued for execution.
    /// </summary>
    public int QueuedCount => Volatile.Read(ref _queuedCount);

    /// <summary>
    /// Gets the number of operations that have been executed.
    /// </summary>
    public int ExecutedCount => Volatile.Read(ref _executedCount);

    /// <summary>
    /// Signals that no more operations will be queued and waits for all pending operations to complete.
    /// </summary>
    public async Task CompleteAndDrainAsync()
    {
        // Only complete once
        if (_channel.Writer.TryComplete())
        {
            try
            {
                await _readerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }
    }

    private async Task ProcessOperationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var request in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await request.ExecuteAsync(_service).ConfigureAwait(false);
                
                // Increment executed counter
                Interlocked.Increment(ref _executedCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            // Unexpected error in the reader loop
            // This should not happen as individual operations handle their own exceptions
            Console.Error.WriteLine($"Unexpected error in SerializedServiceExecutor reader loop: {ex}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Signal shutdown
        _shutdownCts.Cancel();
        
        // Only complete the channel if not already completed
        _channel.Writer.TryComplete();

        try
        {
            // Wait for the reader task to complete
            await _readerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }

        _shutdownCts.Dispose();
    }

    /// <summary>
    /// Represents a queued operation.
    /// </summary>
    private sealed class OperationRequest
    {
        private readonly Func<TService, Task> _operation;
        private readonly CancellationToken _cancellationToken;

        public OperationRequest(
            Func<TService, Task> operation,
            CancellationToken cancellationToken)
        {
            _operation = operation;
            _cancellationToken = cancellationToken;
        }

        public async Task ExecuteAsync(TService service)
        {
            // Check if cancelled before executing
            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await _operation(service).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
            {
                // Operation was cancelled, swallow the exception
            }
            catch (Exception ex)
            {
                // Log the exception but don't propagate since caller isn't waiting
                Console.Error.WriteLine($"Exception in serialized operation: {ex}");
            }
        }
    }
}
