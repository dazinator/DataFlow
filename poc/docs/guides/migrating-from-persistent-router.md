# Migrating from AddPersistentRouter

**Last Updated**: 2026-03-17  
**Difficulty**: Intermediate  
**Prerequisites**: [Selective Routing](topology-selective-routing.md), [Working with Blocks](working-with-blocks.md), [Dependency Injection](dependency-injection-registration.md)

---

## Overview

The legacy DataFlow library's `AddPersistentRouter` primitive dynamically creates a new sub-flow
for each distinct route key encountered in a stream at runtime. The POC DataFlow library uses a
**static graph topology** — all blocks and connections are declared at DI registration time.

This guide explains how to migrate flows that use `AddPersistentRouter` to the POC library,
covering the full range of scenarios from simple to complex.

### What You'll Learn

- Why the POC uses static topology (and why that's a good thing)
- How to re-frame the problem so static topology is sufficient
- Three migration approaches with code examples
- How to choose the right approach for your use case

---

## Table of Contents

1. [Understanding the Difference](#understanding-the-difference)
2. [Re-Framing: Type vs. Tenant](#re-framing-type-vs-tenant)
3. [Approach A: Pre-Enumerate at Startup](#approach-a-pre-enumerate-at-startup)
4. [Approach B: Generic Handler with DI Polymorphism](#approach-b-generic-handler-with-di-polymorphism)
5. [Approach C: Dispatcher Block with Internal Channels](#approach-c-dispatcher-block-with-internal-channels)
6. [Decision Guide](#decision-guide)
7. [Migration Checklist](#migration-checklist)
8. [Worked Example: Journal Processing Flow](#worked-example-journal-processing-flow)

---

## Understanding the Difference

### What AddPersistentRouter Did

In the legacy library, `AddPersistentRouter` created a dynamic sub-flow for each distinct key
it encountered:

```csharp
// Legacy: dynamic sub-flow per ERP system name
.AddPersistentRouter<SystemJournalProcessingItems>(
    routeSelector: item => item.System?.Name ?? "unrouted",
    subFlowBuilder: (builder, systemName) =>
    {
        builder
            .AddTransform<SapBtpConnectorErpJournalSender>(
                sp => ActivatorUtilities.CreateInstance(sp, systemName))
            .AddBatch(maxBatchSize: 200, windowPeriod: TimeSpan.FromSeconds(2))
            .AddTransform<TmsJournalStatusSender>();
    });
```

When the first item with `System.Name = "SAP"` arrived, a complete sub-flow was created for SAP.
When the first item with `System.Name = "Oracle"` arrived later, a separate sub-flow was created
for Oracle. This happened at runtime, based on data.

### Why the POC Uses Static Topology

The POC library requires all blocks and connections to be declared before the graph runs. This
is intentional and provides important benefits:

- **Predictable startup** — the graph is fully wired before processing begins
- **Complete observability** — all blocks visible in visualization and metrics from the start
- **Safe shutdown** — channel completion propagates cleanly through a known topology
- **Compile-time validation** — type mismatches caught at registration, not at runtime
- **Simpler reasoning** — the graph doesn't change shape during execution

The constraint is real, but most `AddPersistentRouter` use cases can be solved within it.

---

## Re-Framing: Type vs. Tenant

The most important step in migration is distinguishing between two kinds of "new route":

| Kind | Example | What drives it |
|------|---------|----------------|
| **New integration type** | Adding Oracle support | Code change (new handler implementation) |
| **New tenant instance** | Customer adds another SAP environment | Configuration change only |

**Legacy routers often created sub-flows per tenant instance, not per integration type.**

If `System.Name` values were `"SAP-Tenant1"`, `"SAP-Tenant2"`, `"SAP-Tenant3"`, the underlying
processing logic was identical — only the target connection details differed. In the POC model:

- **Integration type** → different block implementations → static routing by type
- **Tenant instance** → same block, different configuration → `IOptionsSnapshot<T>`

Once you make this distinction, the problem usually reduces to a **small, known set of integration
types** — which fits perfectly in a static graph.

---

## Approach A: Pre-Enumerate at Startup

**Use when**: You have a bounded, stable set of route destinations (< 20) and can enumerate them
at startup.

Enumerate all known routes from configuration or the database during DI registration. Create a
static selective routing edge for each.

```csharp
builder.Services.AddDataFlows("my-flow", df =>
{
    // Load known systems at startup (blocking call is acceptable during DI setup)
    var systems = builder.Configuration.GetSection("ErpSystems").Get<string[]>()
        ?? Array.Empty<string>();

    // Always include the fallback route
    df.AddActorBlock<SystemItem, PostingResult,
        UnroutedHandler>("unrouted-handler");

    // One block per known system
    foreach (var systemName in systems)
    {
        df.AddActorBlock<SystemItem, PostingResult,
            SapErpHandler>($"handler-{systemName}");
    }

    // Build route mapping
    df.AddGraph("flow", g =>
    {
        var routes = systems
            .Select(s => ($"handler-{s}", (Func<string, bool>)(key => key == s)))
            .ToList();

        g.UseBlock("transformer")
         .RouteBy(item => item.System?.Name ?? "unrouted")
         .To("unrouted-handler", key => key == "unrouted");

        foreach (var (blockName, predicate) in routes)
        {
            // Add each route dynamically at registration time
        }
    });
});
```

**Handling new systems**: When a new system is added to the configuration, restart the
application. The graph re-enumerates and the new route is available.

✅ **Best for**: Small, stable system sets where restart-on-add is acceptable.

---

## Approach B: Generic Handler with DI Polymorphism

**Use when**: The set of integration types is small and known at build time, but new tenants of
those types can be added at runtime without a restart.

This is the **recommended default** for most `AddPersistentRouter` migrations.

### Step 1: Define the Handler Interface

```csharp
/// <summary>
/// Handler for one ERP integration type. Register one implementation per type.
/// </summary>
public interface INamedErpSystemHandler
{
    /// <summary>System type name — must match values returned by the route selector.</summary>
    string SystemName { get; }

    IAsyncEnumerable<PostingResult> PostAsync(
        SystemItem item,
        CancellationToken cancellationToken);
}
```

### Step 2: Implement Handlers per Integration Type

```csharp
// One handler per ERP integration type, not per tenant
public class SapBtpErpHandler : INamedErpSystemHandler
{
    public string SystemName => "SAP";  // Matches System.Name values for SAP

    private readonly ISapConnectorClient _client;
    private readonly ISapPostRequestMapper _mapper;

    public SapBtpErpHandler(ISapConnectorClient client, ISapPostRequestMapper mapper)
    {
        _client = client;
        _mapper = mapper;
    }

    public async IAsyncEnumerable<PostingResult> PostAsync(
        SystemItem item,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // item.System carries per-tenant details (connection string, subscription key, etc.)
        // No need for systemName constructor injection — it's on the item!
        foreach (var journal in item.Journals)
        {
            var request = _mapper.Map(journal, item.System);
            var response = await _client.ODataPostAsync(request, ct);
            yield return new PostingResult(journal, response.Success);
        }
    }
}

// Fallback — items with no ERP system
public class UnroutedErpHandler : INamedErpSystemHandler
{
    public string SystemName => "unrouted";

    public async IAsyncEnumerable<PostingResult> PostAsync(
        SystemItem item,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var journal in item.Journals)
        {
            ct.ThrowIfCancellationRequested();
            yield return new PostingResult(journal, Success: false, IsUnrouted: true);
            await Task.Yield();
        }
    }
}
```

### Step 3: Implement the Dispatcher Actor

```csharp
/// <summary>
/// Single actor block that replaces AddPersistentRouter.
/// Resolves the correct handler at call time based on item content.
/// </summary>
public class ErpPostingActor : IActor<SystemItem, PostingResult>
{
    private readonly IReadOnlyDictionary<string, INamedErpSystemHandler> _handlers;
    private readonly ILogger<ErpPostingActor> _logger;

    public ErpPostingActor(
        IEnumerable<INamedErpSystemHandler> handlers,
        ILogger<ErpPostingActor> logger)
    {
        _handlers = handlers.ToDictionary(h => h.SystemName);
        _logger = logger;
    }

    public async IAsyncEnumerable<PostingResult> ProcessAsync(
        SystemItem item,
        IActorExecutionContext ctx,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var systemName = item.System?.Name ?? "unrouted";

        if (!_handlers.TryGetValue(systemName, out var handler))
        {
            _logger.LogWarning(
                "No handler for system '{SystemName}'. Routing to unrouted.",
                systemName);
            systemName = "unrouted";
            handler = _handlers["unrouted"];
        }

        await foreach (var result in handler.PostAsync(item, ct))
            yield return result;
    }
}
```

### Step 4: Register in DI

```csharp
// Register handlers — one per ERP INTEGRATION TYPE
services.AddScoped<INamedErpSystemHandler, SapBtpErpHandler>();
services.AddScoped<INamedErpSystemHandler, UnroutedErpHandler>();
// services.AddScoped<INamedErpSystemHandler, OracleErpHandler>(); // When Oracle is added

services.AddScoped<ErpPostingActor>();

// Graph: simple linear pipeline
df.AddActorBlock<SystemItem, PostingResult,
    ErpPostingActor>("erp-poster", maxConcurrency: 3);

df.AddGraph("flow", g =>
{
    g.UseBlock("routing-transformer")
     .ProcessWith("erp-poster")       // Single block — replaces entire AddPersistentRouter subtree
     .BatchWith("tms-batcher")
     .ProcessWith("tms-sender");
});
```

### New Tenants at Runtime

When a customer adds a new SAP environment:

1. Add the company-code → system mapping to the database.
2. Add the per-tenant configuration (connection string, subscription key) to the tenant config store.
3. The routing transformer pre-loads the new mapping on the next execution.
4. `SapBtpErpHandler` reads the per-tenant config from `IOptionsSnapshot<T>` using
   `item.System` details — **no restart needed**.

✅ **Best for**: Most `AddPersistentRouter` migrations. Small number of ERP types, dynamic tenant configuration.

---

## Approach C: Dispatcher Block with Internal Channels

**Use when**: New integration types (not just tenants) can be added at runtime without a
deployment, and you cannot require a restart.

This is the most complex approach and should only be chosen when the simpler approaches are
genuinely insufficient.

### How it Works

A single actor block maintains an internal `ConcurrentDictionary` of per-system `Channel<T>`
pipelines. When a new system key is encountered, a new channel and background task are created.

```csharp
public class DynamicErpDispatcher
{
    private readonly IErpHandlerFactory _factory;
    private readonly ILogger<DynamicErpDispatcher> _logger;

    public async IAsyncEnumerable<PostingResult> ProcessStreamAsync(
        IAsyncEnumerable<SystemItem> input,
        IServiceProvider sp,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var pipelines = new Dictionary<string, Pipeline>();
        var outputCh = Channel.CreateBounded<PostingResult>(500);

        try
        {
            await foreach (var item in input.WithCancellation(ct))
            {
                var key = item.System?.Name ?? "unrouted";

                if (!pipelines.TryGetValue(key, out var pipeline))
                {
                    _logger.LogInformation("Creating pipeline for '{Key}'", key);
                    pipeline = CreatePipeline(key, outputCh.Writer, sp, ct);
                    pipelines[key] = pipeline;
                }

                await pipeline.Input.WriteAsync(item, ct);
            }
        }
        finally
        {
            foreach (var p in pipelines.Values)
                p.Input.TryComplete();
        }

        await Task.WhenAll(pipelines.Values.Select(p => p.Completion));
        outputCh.Writer.TryComplete();

        await foreach (var result in outputCh.Reader.ReadAllAsync(ct))
            yield return result;
    }

    private Pipeline CreatePipeline(
        string key,
        ChannelWriter<PostingResult> outputWriter,
        IServiceProvider sp,
        CancellationToken ct)
    {
        var inputCh = Channel.CreateBounded<SystemItem>(200);
        var handler = _factory.CreateHandler(key, sp);

        var completion = Task.Run(async () =>
        {
            await foreach (var item in inputCh.Reader.ReadAllAsync(ct))
            {
                await foreach (var result in handler.PostAsync(item, ct))
                    await outputWriter.WriteAsync(result, ct);
            }
        }, ct);

        return new Pipeline(inputCh.Writer, completion);
    }

    private record Pipeline(ChannelWriter<SystemItem> Input, Task Completion);
}

public interface IErpHandlerFactory
{
    INamedErpSystemHandler CreateHandler(string systemKey, IServiceProvider sp);
}
```

⚠️ **Note**: Per-system pipelines are **not visible** to the graph visualization or metrics
system. You must implement internal logging and metrics to compensate.

✅ **Best for**: Unbounded or frequently changing system sets where restart is genuinely not possible.

---

## Decision Guide

| Scenario | Approach |
|----------|----------|
| Small, stable set of ERP systems (< 10, restart acceptable) | **A: Pre-Enumerate** |
| Known ERP types, new tenants via config at runtime | **B: Generic Handler** ⭐ Default |
| New ERP types added at runtime without deployment | **C: Dispatcher Block** |
| Very complex per-route sub-graphs (> 3 blocks, independent lifecycle) | Consider filing a new research issue |

**Quick check**: If adding a new "route" in the legacy system was triggered by a new tenant
onboarding (not a code change), use **Approach B** — it's almost certainly the right answer.

---

## Migration Checklist

- [ ] Identify what `AddPersistentRouter` route keys represent (types vs. tenants)
- [ ] List the integration types (SAP, Oracle, etc.) — these become handler implementations
- [ ] Implement `INamedErpSystemHandler` per integration type
- [ ] Implement dispatcher actor (Approach B) or enumerate routes at startup (Approach A)
- [ ] Remove per-route block instantiation patterns (e.g., `ActivatorUtilities.CreateInstance`)
- [ ] Replace constructor-injected system name with `item.System` access in handler
- [ ] Ensure "unrouted" fallback preserves existing business rules (e.g., still flows through TMS)
- [ ] Register all handlers in DI with `AddScoped<INamedErpSystemHandler, MyHandler>()`
- [ ] Verify per-tenant configuration uses `IOptionsSnapshot<T>`, not constructor arguments
- [ ] Test that new tenants are handled without restart (Approach B) or with restart (Approach A)

---

## Worked Example: Journal Processing Flow

The following shows the complete migration of the legacy journal processing flow, which used
`AddPersistentRouter` to route to SAP and unrouted paths.

### Legacy Topology (Before)

```
[Producer] → [Batch] → [RateLimit] → [RoutingTransformer]
    → [PersistentRouter: by System.Name]
        ├── "unrouted" → [UnroutedTransform] → [Batch] → [TmsSender]
        └── "SAP"      → [SapSender(SAP)]    → [Batch] → [TmsSender]
```

### POC Topology (After — Approach B)

```
[Producer] → [Batch] → [RoutingTransformer] → [ErpPostingActor] → [Batch] → [TmsSender]
```

The `ErpPostingActor` resolves `SapBtpErpHandler` or `UnroutedErpHandler` by `item.System?.Name`.

### Key Changes

| Legacy Pattern | POC Pattern |
|----------------|-------------|
| Sub-flow per system | Single `ErpPostingActor` block |
| `ActivatorUtilities.CreateInstance(sp, systemName)` | Handler reads `item.System` directly |
| Per-route `TmsBatcher` + `TmsSender` | Shared TMS path after dispatcher |
| `MaxConcurrency=3` on per-route transform | `maxConcurrency: 3` on `ErpPostingActor` |
| Unknown route → no sub-flow | Unknown type → `UnroutedErpHandler` fallback |

### DI Registration

```csharp
services.AddScoped<INamedErpSystemHandler, SapBtpErpHandler>();
services.AddScoped<INamedErpSystemHandler, UnroutedErpHandler>();
services.AddScoped<ErpPostingActor>();

df.AddGraph("journal-flow", g =>
{
    g.UseBlock("producer")
     .BatchWith("journal-batcher")
     .ProcessWith("routing-transformer")
     .ProcessWith("erp-poster")          // maxConcurrency: 3
     .BatchWith("tms-batcher")
     .ProcessWith("tms-sender");
});
```

---

## Next Steps

- **[Selective Routing Guide](topology-selective-routing.md)** — Static routing for known route sets
- **[Competing Consumers](topology-competing-consumers.md)** — Load balancing within a route
- **[Dependency Injection](dependency-injection-registration.md)** — DI patterns for blocks
- **[Using Epochs](using-epochs.md)** — Transaction boundaries for flow executions

---

**Document Version**: 1.0  
**Status**: ✅ Current  
**Last Updated**: 2026-03-17
