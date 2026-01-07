namespace Uniun.DataFlow.Blocks.Broadcast;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Builder.Graph;

/// <summary>
/// A broadcast block that takes input items and fans them out to multiple downstream blocks concurrently.
/// Each subscriber receives all items from the source.
/// 
/// An optional cloning function can be provided to create separate instances for each subscriber,
/// avoiding concurrent access issues with mutable objects. If no clone function is provided,
/// the same object reference is passed to all subscribers (suitable only for immutable types).
/// 
/// Per-target clone functions can be configured using ConfigureTarget() for fine-grained control.
/// </summary>
/// <typeparam name="T">The type of items to broadcast</typeparam>
public class BroadcastBlock<T> : BlockBase, ITargetBlock<T>, ISourceBlock<T>, IDataFlowInitializable
{
    private readonly ILogger<BroadcastBlock<T>> _logger;
    private readonly Func<T, T>? _defaultCloneFunc;
    private readonly Dictionary<string, Func<T, T>?> _targetCloneFuncs = new();
    private readonly Dictionary<ITargetBlock<T>, BroadcastTarget<T>> _connectedTargets = new();
    private readonly object _connectionLock = new();
    private readonly TaskCompletionSource<bool> _allTargetsConnectedTcs = new();
    private ISourceBlock<T>? _source;
    private int _expectedTargetCount;
    private int _connectedTargetCount;

    public BroadcastBlock(
        string name,
        ILogger<BroadcastBlock<T>> logger,
        Func<T, T>? defaultCloneFunc = null,
        BlockOptions? options = null) : base(name, options, logger)
    {
        _logger = logger;
        _defaultCloneFunc = defaultCloneFunc;
    }

    /// <summary>
    /// Configures a clone function for a specific target block by name.
    /// This allows different targets to have different cloning strategies.
    /// If not configured for a target, the default clone function is used.
    /// </summary>
    /// <param name="targetName">The name of the target block</param>
    /// <param name="cloneFunc">The clone function for this target, or null to not clone</param>
    public void ConfigureTarget(string targetName, Func<T, T>? cloneFunc)
    {
        lock (_connectionLock)
        {
            _targetCloneFuncs[targetName] = cloneFunc;
            _logger.LogDebug(
                "Configured target '{TargetName}' for broadcast block '{BlockName}' with {CloneStatus}",
                targetName,
                Name,
                cloneFunc == null ? "no cloning" : "cloning");
        }
    }

    public void SetSource(ISourceBlock<T> source)
    {
        _source = source;
    }

    private void EnsureSource()
    {
        if (_source is null)
        {
            throw new InvalidOperationException($"No source block configured for BroadcastBlock '{Name}'");
        }
    }

    /// <summary>
    /// Called during dataflow initialization to discover the expected number of downstream targets.
    /// This prevents the race condition where broadcasting starts before all targets have connected.
    /// </summary>
    public override void OnDataFlowInitialized(IDataFlowRuntimeGraph runtimeGraph, CancellationToken cancellationToken)
    {
        // Count how many blocks are expecting to receive from this broadcast block
        _expectedTargetCount = runtimeGraph.Graph.Connections
            .Count(c => c.SourceBlockName == Name);

        _logger.LogDebug(
            "BroadcastBlock '{BlockName}' initialized. Expected target count: {ExpectedTargetCount}",
            Name,
            _expectedTargetCount);

        // If no targets are expected, complete immediately
        if (_expectedTargetCount == 0)
        {
            _allTargetsConnectedTcs.TrySetResult(true);
        }
    }

    /// <summary>
    /// This method is called when a downstream block connects to this broadcast block.
    /// It creates a dedicated channel for that target and registers it for broadcasting.
    /// Items will be cloned if a clone function was provided to avoid concurrent access issues.
    /// </summary>
    public IAsyncEnumerable<T> GetAsyncEnumerable(ITargetBlock<T> target, CancellationToken cancellationToken)
    {
        BroadcastTarget<T> broadcastTarget;

        lock (_connectionLock)
        {
            if (!_connectedTargets.TryGetValue(target, out broadcastTarget!))
            {
                // Create a new channel for this target
                // Use capacity of 1 to maintain backpressure without buffering.
                // The goal is flow control, not buffering. If developers want buffering,
                // they can add a BufferBlock downstream as a separate block with configurable size.
                var channelOptions = new BoundedChannelOptions(1)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleWriter = true, // Only this broadcast block writes to each channel
                    SingleReader = true  // Only the target block reads from its channel
                };

                var channel = Channel.CreateBounded<T>(channelOptions);

                var targetName = target.Name ?? $"Target-{_connectedTargets.Count + 1}";

                // Determine the clone function for this target
                // 1. If a specific clone function was configured for this target, use it
                // 2. Otherwise, use the default clone function
                var cloneFunc = _defaultCloneFunc;
                if (_targetCloneFuncs.TryGetValue(targetName, out var targetSpecificClone))
                {
                    cloneFunc = targetSpecificClone;
                    _logger.LogDebug(
                        "Using target-specific clone function for '{TargetName}' in broadcast block '{BlockName}'",
                        targetName,
                        Name);
                }

                broadcastTarget = new BroadcastTarget<T>
                {
                    TargetBlock = target,
                    Channel = channel,
                    TargetName = targetName,
                    CloneFunc = cloneFunc
                };

                _connectedTargets[target] = broadcastTarget;
                _connectedTargetCount++;

                _logger.LogDebug(
                    "Connected new target '{TargetName}' to broadcast block '{BlockName}'. Total targets: {TargetCount}/{ExpectedCount}",
                    broadcastTarget.TargetName,
                    Name,
                    _connectedTargets.Count,
                    _expectedTargetCount);

                // Signal if all expected targets have connected
                if (_connectedTargetCount >= _expectedTargetCount && _expectedTargetCount > 0)
                {
                    _allTargetsConnectedTcs.TrySetResult(true);
                    _logger.LogDebug(
                        "All expected targets connected to broadcast block '{BlockName}'",
                        Name);
                }
            }
        }

        return ReadFromChannelAsync(broadcastTarget.Channel.Reader, cancellationToken);
    }

    private async IAsyncEnumerable<T> ReadFromChannelAsync(
        ChannelReader<T> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        EnsureSource();

        // Wait for all expected targets to connect before starting to broadcast
        // This prevents the race condition where items are broadcast before all targets are ready
        if (_expectedTargetCount > 0)
        {
            _logger.LogDebug(
                "BroadcastBlock '{BlockName}' waiting for {ExpectedCount} targets to connect...",
                Name,
                _expectedTargetCount);

            await _allTargetsConnectedTcs.Task;

            _logger.LogDebug(
                "BroadcastBlock '{BlockName}' starting broadcast with {TargetCount} connected targets",
                Name,
                _connectedTargets.Count);
        }

        // If no targets connected, log warning but don't fail
        if (_connectedTargets.Count == 0)
        {
            _logger.LogWarning(
                "BroadcastBlock '{BlockName}' has no connected targets. Items will be consumed but not processed.",
                Name);
        }

        try
        {
            // Process items and broadcast to all targets
            await BroadcastItemsAsync(context);
        }
        finally
        {
            // Complete all target channels
            CompleteAllTargets();
        }
    }

    private async Task BroadcastItemsAsync(IDataFlowContext context)
    {
        await foreach (var item in _source!.GetAsyncEnumerable(this, context.CancellationToken))
        {
            try
            {
                // Write to all connected targets concurrently
                // Clone the item for each target if a clone function is provided
                await BroadcastItemToAllTargetsAsync(item, context.CancellationToken);
                RecordOperation();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting item in block '{BlockName}'", Name);
                throw;
            }
        }
    }

    private async Task BroadcastItemToAllTargetsAsync(T item, CancellationToken cancellationToken)
    {
        if (_connectedTargets.Count == 0)
        {
            return; // No targets to broadcast to
        }

        // Create write tasks for all targets
        var writeTasks = new List<Task>(_connectedTargets.Count);

        lock (_connectionLock)
        {
            foreach (var target in _connectedTargets.Values)
            {
                // Use the target-specific clone function (which may be null for no cloning)
                var itemToSend = target.CloneFunc != null ? target.CloneFunc(item) : item;
                writeTasks.Add(WriteToTargetAsync(target, itemToSend, cancellationToken));
            }
        }

        // Wait for all writes to complete (slowest target determines throughput)
        await Task.WhenAll(writeTasks);
    }

    private async Task WriteToTargetAsync(
        BroadcastTarget<T> target,
        T item,
        CancellationToken cancellationToken)
    {
        // Write to the target's channel (respects backpressure)
        // This may wait if the channel is full, but won't fail under normal circumstances.
        // If the flow is cancelled, OperationCanceledException will propagate naturally.
        await target.Channel.Writer.WriteAsync(item, cancellationToken);

        _logger.LogTrace(
            "Wrote item to target '{TargetName}' in block '{BlockName}'",
            target.TargetName,
            Name);
    }

    private void CompleteAllTargets()
    {
        lock (_connectionLock)
        {
            foreach (var target in _connectedTargets.Values)
            {
                try
                {
                    target.Channel.Writer.Complete();
                    _logger.LogDebug(
                        "Completed target '{TargetName}' in block '{BlockName}'",
                        target.TargetName,
                        Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error completing target '{TargetName}' in block '{BlockName}'",
                        target.TargetName,
                        Name);
                }
            }
        }
    }

    /// <summary>
    /// Represents a target that is connected to this broadcast block.
    /// </summary>
    private class BroadcastTarget<TItem>
    {
        public required ITargetBlock<TItem> TargetBlock { get; init; }
        public required Channel<TItem> Channel { get; init; }
        public required string TargetName { get; init; }
        public required Func<TItem, TItem>? CloneFunc { get; init; }
    }
}
