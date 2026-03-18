// ============================================================
// DispatcherBlockPrototype.cs
// Prototype: ErpPostingActor — dispatches to system-type handlers
//
// This is REFERENCE CODE for the implementation team.
// ============================================================

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace JournalProcessing.Prototype;

/// <summary>
/// Actor block that replaces the legacy AddPersistentRouter pattern.
///
/// Instead of creating one sub-flow per ERP system, this actor:
/// 1. Receives all SystemJournalProcessingItems (regardless of system).
/// 2. Resolves the appropriate INamedErpSystemHandler by system type name.
/// 3. Falls back to UnroutedJournalHandler when no matching handler exists.
/// 4. Yields all JournalPostingResult items to the shared downstream TMS path.
///
/// This block is registered ONCE in DI. All ERP system types are handled
/// by the same block instance; new tenants don't require new blocks or routes.
///
/// CONCURRENCY: Configure MaxConcurrency on this block to process multiple
/// SystemJournalProcessingItems in parallel. Each concurrent worker handles
/// one item (one system's journals) at a time. This reproduces the
/// MaxConcurrency=3 from the legacy SAP sender.
/// </summary>
public sealed class ErpPostingActor : IStreamActor<SystemJournalProcessingItems, JournalPostingResult>
    // IStreamActor<TIn, TOut> is the correct interface in the current POC.
    // See /poc/DataFlow/Core/IStreamActor.cs for the full interface definition.
{
    private readonly IReadOnlyDictionary<string, INamedErpSystemHandler> _handlers;
    private readonly UnroutedJournalHandler _unroutedHandler;
    private readonly ILogger<ErpPostingActor> _logger;

    /// <summary>
    /// Constructor injection — all registered INamedErpSystemHandler implementations
    /// are resolved from DI and indexed by SystemName.
    /// </summary>
    /// <param name="handlers">All registered ERP handlers (SAP, Oracle, etc.)</param>
    /// <param name="unroutedHandler">Fallback for items with no ERP system</param>
    /// <param name="logger">Logger</param>
    public ErpPostingActor(
        IEnumerable<INamedErpSystemHandler> handlers,
        UnroutedJournalHandler unroutedHandler,
        ILogger<ErpPostingActor> logger)
    {
        _handlers = handlers.ToDictionary(h => h.SystemName);
        _unroutedHandler = unroutedHandler;
        _logger = logger;
    }

    /// <summary>
    /// Processes the input stream by routing each item to the correct handler.
    ///
    /// Implements IStreamActor<TIn, TOut>.RunAsync — takes the full input stream
    /// and yields output items. The item.System?.Name gives us the route key.
    /// </summary>
    public async IAsyncEnumerable<JournalPostingResult> RunAsync(
        IAsyncEnumerable<SystemJournalProcessingItems> input,
        IActorExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
        var systemName = item.System?.Name;

        // Resolve handler by system type name
        INamedErpSystemHandler? handler = null;
        if (systemName != null && !_handlers.TryGetValue(systemName, out handler))
        {
            // System name present but no handler registered for this type
            // This happens when a new ERP integration type is added to the DB
            // but no code handler exists yet — treat as unrouted and log a warning.
            _logger.LogWarning(
                "No ERP handler registered for system type '{SystemName}'. " +
                "Routing to unrouted handler. " +
                "Register an INamedErpSystemHandler with SystemName = '{SystemName}' to handle this.",
                systemName, systemName);
        }

        // Use unrouted handler if:
        // - item.System is null (no company-code mapping found in routing transformer)
        // - system name present but no handler registered for that type
        if (handler is null)
        {
            await foreach (var result in _unroutedHandler.HandleAsync(item, context.CancellationToken))
            {
                yield return result;
            }
            continue;
        }

        // Delegate to the matched handler
        await foreach (var result in handler.PostAsync(item, context.CancellationToken))
        {
            yield return result;
        }
        } // end foreach
    }
}

// ============================================================
// Approach B prototype — Dispatcher with internal channels
// (For reference only — use Approach C unless truly dynamic
//  routes are required)
// ============================================================

/// <summary>
/// Alternative dispatcher that creates per-system channels at runtime.
/// Use this ONLY when new ERP system TYPES (not just tenants) can appear at runtime.
///
/// This is considerably more complex than ErpPostingActor and should only be
/// chosen when Approach C genuinely cannot meet the requirements.
/// </summary>
public sealed class DynamicErpDispatcherActor
{
    private readonly IErpHandlerFactory _handlerFactory;
    private readonly ILogger<DynamicErpDispatcherActor> _logger;

    public DynamicErpDispatcherActor(
        IErpHandlerFactory handlerFactory,
        ILogger<DynamicErpDispatcherActor> logger)
    {
        _handlerFactory = handlerFactory;
        _logger = logger;
    }

    public async IAsyncEnumerable<JournalPostingResult> HandleStreamAsync(
        IAsyncEnumerable<SystemJournalProcessingItems> inputStream,
        IServiceProvider serviceProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Per-system internal pipelines created on demand
        var pipelines = new Dictionary<string, SystemPipeline>();

        // Output channel that all per-system pipelines write to
        var outputChannel = System.Threading.Channels.Channel.CreateBounded<JournalPostingResult>(
            new System.Threading.Channels.BoundedChannelOptions(500)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
            });

        var inputTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in inputStream.WithCancellation(cancellationToken))
                {
                    var systemKey = item.System?.Name ?? "unrouted";

                    if (!pipelines.TryGetValue(systemKey, out var pipeline))
                    {
                        _logger.LogInformation("Creating pipeline for system '{SystemKey}'", systemKey);
                        pipeline = CreateSystemPipeline(
                            systemKey, outputChannel.Writer, serviceProvider, cancellationToken);
                        pipelines[systemKey] = pipeline;
                    }

                    await pipeline.InputWriter.WriteAsync(item, cancellationToken);
                }
            }
            finally
            {
                // Signal all pipelines no more input
                foreach (var pipeline in pipelines.Values)
                    pipeline.InputWriter.TryComplete();
            }
        }, cancellationToken);

        // Wait for input enumeration to complete, then wait for all pipelines to drain
        await inputTask;
        await Task.WhenAll(pipelines.Values.Select(p => p.CompletionTask));
        outputChannel.Writer.TryComplete();

        // Yield all results
        await foreach (var result in outputChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return result;
        }
    }

    private SystemPipeline CreateSystemPipeline(
        string systemKey,
        System.Threading.Channels.ChannelWriter<JournalPostingResult> outputWriter,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var inputChannel = System.Threading.Channels.Channel.CreateBounded<SystemJournalProcessingItems>(
            new System.Threading.Channels.BoundedChannelOptions(200)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
            });

        var handler = _handlerFactory.CreateHandler(systemKey, serviceProvider);

        var completionTask = Task.Run(async () =>
        {
            await foreach (var item in inputChannel.Reader.ReadAllAsync(cancellationToken))
            {
                await foreach (var result in handler.PostAsync(item, cancellationToken))
                {
                    await outputWriter.WriteAsync(result, cancellationToken);
                }
            }
        }, cancellationToken);

        return new SystemPipeline(inputChannel.Writer, completionTask);
    }

    private sealed record SystemPipeline(
        System.Threading.Channels.ChannelWriter<SystemJournalProcessingItems> InputWriter,
        Task CompletionTask);
}

/// <summary>
/// Factory for creating system handlers dynamically (used by DynamicErpDispatcherActor only).
/// </summary>
public interface IErpHandlerFactory
{
    INamedErpSystemHandler CreateHandler(string systemKey, IServiceProvider serviceProvider);
}
