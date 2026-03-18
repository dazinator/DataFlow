# Approach B: Dispatcher Block with Internal Concurrency

**Category**: Truly dynamic routes, runtime-created channels  
**Effort**: Medium  
**POC library changes required**: None (self-contained in a user-defined actor block)

---

## Overview

Move the dynamic routing concern **inside a single actor block** rather than expressing it as
graph topology. The dispatcher block:

1. Receives tagged items (`SystemJournalProcessingItems`).
2. Looks up (or creates on first encounter) an internal per-system processing pipeline backed by
   a `Channel<T>`.
3. Writes each item to the matching channel.
4. Each per-system channel has a dedicated background `Task` running the system-specific handler.

This is conceptually equivalent to the legacy `AddPersistentRouter` but expressed as user code
rather than a library primitive.

---

## How it Works

```
[Routing Transformer]
  Emits SystemJournalProcessingItems
      ↓
[ErpDispatcherActor]  ← single block
  ┌─────────────────────────────────────────────────────────────┐
  │  ConcurrentDictionary<string, SystemPipeline>               │
  │                                                             │
  │  "unrouted" → Channel → UnroutedHandler Task               │
  │  "SAP-T1"   → Channel → SapHandler(SAP-T1) Task   ←───────── New channels created
  │  "SAP-T2"   → Channel → SapHandler(SAP-T2) Task   ←───────── on first encounter
  │  "Oracle-T1"→ Channel → OracleHandler Task                 │
  └─────────────────────────────────────────────────────────────┘
      ↓ (all results merged)
  [TmsBatcher] → [TmsSender]
```

---

## Implementation Sketch

```csharp
/// <summary>
/// Actor block that dynamically creates a per-system processing pipeline for each
/// distinct ERP system encountered in the stream.
/// </summary>
public class ErpDispatcherActor : IStreamActor<SystemJournalProcessingItems, JournalPostingResult>
{
    private readonly IErpHandlerFactory _handlerFactory;
    private readonly ILogger<ErpDispatcherActor> _logger;
    
    // Created lazily on first encounter of each system key
    private readonly ConcurrentDictionary<string, SystemPipeline> _pipelines = new();

    public ErpDispatcherActor(IErpHandlerFactory handlerFactory,
        ILogger<ErpDispatcherActor> logger)
    {
        _handlerFactory = handlerFactory;
        _logger = logger;
    }

    public async IAsyncEnumerable<JournalPostingResult> ProcessAsync(
        IAsyncEnumerable<SystemJournalProcessingItems> input,
        IActorExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Output channel — all per-system handlers write results here
        var outputChannel = Channel.CreateBounded<JournalPostingResult>(
            new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.Wait });

        try
        {
            await foreach (var item in input.WithCancellation(cancellationToken))
            {
                var systemKey = item.System?.Name ?? "unrouted";
                
                // Get or create pipeline for this system
                var pipeline = _pipelines.GetOrAdd(systemKey, key =>
                {
                    _logger.LogInformation("Creating new ERP pipeline for system {System}", key);
                    return CreatePipeline(key, outputChannel.Writer, context, cancellationToken);
                });

                // Route item to system-specific channel
                await pipeline.InputWriter.WriteAsync(item, cancellationToken);
            }

            // Signal all pipelines that no more input is coming
            foreach (var pipeline in _pipelines.Values)
            {
                pipeline.InputWriter.Complete();
            }

            // Wait for all pipeline tasks to finish
            await Task.WhenAll(_pipelines.Values.Select(p => p.CompletionTask));
        }
        finally
        {
            outputChannel.Writer.TryComplete();
        }

        // Yield all results collected by the output channel
        await foreach (var result in outputChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return result;
        }
    }

    private SystemPipeline CreatePipeline(
        string systemKey,
        ChannelWriter<JournalPostingResult> outputWriter,
        IActorExecutionContext context,
        CancellationToken cancellationToken)
    {
        var inputChannel = Channel.CreateBounded<SystemJournalProcessingItems>(
            new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.Wait });

        // Resolve the appropriate handler for this system type
        var handler = _handlerFactory.CreateHandler(systemKey, context.ServiceProvider);

        var completionTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in inputChannel.Reader.ReadAllAsync(cancellationToken))
                {
                    await foreach (var result in handler.HandleAsync(item, cancellationToken))
                    {
                        await outputWriter.WriteAsync(result, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation
            }
        }, cancellationToken);

        return new SystemPipeline(inputChannel.Writer, completionTask);
    }

    private record SystemPipeline(ChannelWriter<SystemJournalProcessingItems> InputWriter,
        Task CompletionTask);
}

/// <summary>
/// Factory that creates system-specific ERP handlers.
/// New system types can be registered without modifying the dispatcher.
/// </summary>
public interface IErpHandlerFactory
{
    IErpSystemHandler CreateHandler(string systemKey, IServiceProvider serviceProvider);
}

/// <summary>
/// Handler for one ERP system instance. Implemented per ERP type (SAP, Oracle, etc.)
/// </summary>
public interface IErpSystemHandler
{
    IAsyncEnumerable<JournalPostingResult> HandleAsync(
        SystemJournalProcessingItems item,
        CancellationToken cancellationToken);
}
```

### Graph Registration

```csharp
df.AddActorBlock<SystemJournalProcessingItems, JournalPostingResult,
    ErpDispatcherActor>("erp-dispatcher");

df.AddBatchBlock<JournalPostingResult>("tms-batcher", ...);
df.AddActorBlock<JournalPostingResult[], Unit, TmsJournalStatusSender>("tms-sender");

df.AddGraph("journal-flow", g =>
{
    g.UseBlock("producer")
     .BatchWith("journal-batcher")
     .RateLimitWith("rate-limiter")
     .ProcessWith("routing-transformer")
     .ProcessWith("erp-dispatcher")     // Single block, internal dynamic routing
     .BatchWith("tms-batcher")
     .ProcessWith("tms-sender");
});
```

---

## Shutdown and Cancellation

When the dataflow graph shuts down:

1. `cancellationToken` is cancelled → `input` enumeration ends.
2. Dispatcher completes all internal channel writers.
3. `await Task.WhenAll(...)` waits for all per-system tasks to drain.
4. Output channel is completed → downstream blocks see end of stream.

This provides **clean, ordered shutdown** — equivalent to the static topology case.

---

## Observability

Because the per-system pipelines are **internal** to the actor block, they are not visible in the
graph visualization or via graph-level metrics. Compensate with:

- Per-system internal logging (already shown in `CreatePipeline`).
- Internal `Channel` metrics (items pending, throughput per system).
- OpenTelemetry spans emitted from the handler factory.

---

## Pros and Cons

| | |
|---|---|
| ✅ **Truly dynamic** | New systems at runtime, zero restart required |
| ✅ **No library changes** | Fully expressible as user-space actor code |
| ✅ **Correct backpressure** | Bounded channels propagate pressure upstream |
| ✅ **Clean shutdown** | Completion propagates through all internal pipelines |
| ✅ **Handler isolation** | Each system runs in its own task |
| ❌ **Not graph-visible** | Per-system routes invisible to graph visualization |
| ❌ **Manual observability** | Must implement internal metrics and tracing |
| ❌ **Complex error handling** | Per-task failures must be caught and surfaced |
| ❌ **Memory leak risk** | Unbounded `_pipelines` dictionary (mitigate with LRU) |

---

## Mitigations for Key Risks

### Memory Leak: Unbounded Pipelines Dictionary

If a new system key is encountered for every item (pathological case), the dictionary grows
without bound. Mitigate with:

```csharp
// Option: limit total pipelines and evict idle ones
private const int MaxPipelines = 1000;
private readonly LinkedList<string> _lruOrder = new();

// Or: validate system keys upstream before reaching the dispatcher
```

In the journal processing use case, the number of ERP tenants is bounded by business constraints
(hundreds at most), so this is not a practical concern.

### Task Failures

If a per-system task faults, it should:
1. Log the error.
2. Complete its input channel exceptionally.
3. Propagate the exception to the dispatcher's `Task.WhenAll`.
4. The dispatcher surfaces the exception to the graph, triggering graceful shutdown.

---

## When to Choose Approach B

- New routes **must be created at runtime** without an application restart.
- The number of distinct systems is **large or unbounded**.
- You are comfortable with internal-to-block concurrency management.
- Graph-level visualization of sub-routes is **not a hard requirement**.
