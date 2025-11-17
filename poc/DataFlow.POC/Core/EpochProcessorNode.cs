namespace DataFlow.POC.Core;

using System.Threading.Channels;
using DataFlow.POC.Checkpointing;

/// <summary>
/// Node that processes epochs by draining their operation queues and executing lifecycle hooks.
/// Reads epochs from the source node's stream and processes them sequentially.
/// </summary>
public sealed class EpochProcessorNode : IAsyncDisposable
{
    private readonly ChannelReader<IEpoch> _epochReader;
    private readonly EpochHooks _hooks;
    private readonly CancellationTokenSource _cts;
    private readonly Task _processingTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpochProcessorNode"/> class.
    /// </summary>
    /// <param name="source">The epoch source node to read from.</param>
    /// <param name="hooks">Lifecycle hooks to execute during epoch processing.</param>
    public EpochProcessorNode(EpochSourceNode source, EpochHooks? hooks = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        
        _epochReader = source.EpochReader;
        _hooks = hooks ?? new EpochHooks();
        _cts = new CancellationTokenSource();
        _processingTask = Task.Run(() => ProcessEpochsAsync(_cts.Token));
    }

    /// <summary>
    /// Gets the task representing the epoch processing work.
    /// This task completes when all epochs have been processed or an error occurs.
    /// </summary>
    public Task CompletionTask => _processingTask;

    private async Task ProcessEpochsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var epoch in _epochReader.ReadAllAsync(cancellationToken))
            {
                await ProcessEpochAsync(epoch, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cancellation, not an error
        }
    }

    private async Task ProcessEpochAsync(IEpoch epoch, CancellationToken cancellationToken)
    {
        try
        {
            // Execute pre-epoch hook (can queue operations like BeginTransaction)
            if (_hooks.OnBeginEpoch != null)
            {
                await _hooks.OnBeginEpoch(epoch, cancellationToken).ConfigureAwait(false);
            }

            // Drain all currently queued operations from the epoch's queue
            await DrainCurrentlyQueuedOperationsAsync(epoch, cancellationToken).ConfigureAwait(false);
            
            // Execute post-epoch hook (can queue operations like CommitTransaction)
            if (_hooks.OnCommitEpoch != null)
            {
                await _hooks.OnCommitEpoch(epoch, cancellationToken).ConfigureAwait(false);
            }
            
            // Now complete the operations channel - no more operations will be queued
            // In the real graph, this would be called by graph block alignment
            // For now, processor does it after hooks have queued their operations
            epoch.CompleteOperations();
            
            // Drain any final operations queued by OnCommitEpoch
            await DrainCurrentlyQueuedOperationsAsync(epoch, cancellationToken).ConfigureAwait(false);
            
            // Wait for channel to fully complete (all operations drained)
            await epoch.WhenAllOperationsCompletedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Handle error
            if (_hooks.OnEpochError != null)
            {
                try
                {
                    await _hooks.OnEpochError(epoch, ex, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // If error hook fails, just propagate original exception
                }
            }

            // Propagate exception to fail graph execution
            throw;
        }
        finally
        {
            // Always dispose epoch after processing
            await epoch.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task DrainCurrentlyQueuedOperationsAsync(IEpoch epoch, CancellationToken cancellationToken)
    {
        Exception? firstException = null;

        // Epoch implements IEpochOperationContext, so we can use it directly
        // This avoids allocating a new context object per epoch
        var context = epoch as IEpochOperationContext 
            ?? throw new InvalidOperationException("Epoch implementation must implement IEpochOperationContext to provide checkpoint access. This is a framework invariant violation.");
        
        // Process currently queued operations (use TryRead to avoid waiting for channel completion)
        while (epoch.OperationsReader.TryRead(out var operation))
        {
            try
            {
                await operation.ExecuteAsync(epoch.ServiceProvider, context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (firstException == null)
            {
                // Capture first exception but continue draining
                firstException = ex;
            }
        }
        
        // Rethrow first exception if one occurred
        if (firstException != null)
        {
            throw firstException;
        }
    }

    /// <summary>
    /// Disposes the processor and waits for processing to complete.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Cancel processing
        _cts.Cancel();

        try
        {
            // Wait for processing task to complete
            await _processingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during cancellation
        }
        finally
        {
            _cts.Dispose();
        }
    }
}
