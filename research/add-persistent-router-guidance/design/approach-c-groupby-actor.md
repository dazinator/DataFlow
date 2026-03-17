# Approach C: GroupBy Actor with Generic Handler

**Category**: Restructure the problem to fit static topology  
**Effort**: Low–Medium  
**POC library changes required**: None

---

## Overview

Instead of "routing to a sub-graph per system", process all ERP systems through a **single generic
handler** that receives the system context alongside the data. The actor responsible for ERP
posting receives both the journals **and** the target system identifier, and uses the system
identifier to select the correct integration implementation at call time.

This approach eliminates the need for routing entirely by making the handler polymorphic.

---

## How it Works

```
[Routing Transformer]
  Emits SystemJournalProcessingItems (tagged with System)
      ↓
[ErpPostingActor]                      ← single block, handles all systems
  For each item:
    Resolves IErpSystemHandler by item.System.Name
    Calls handler.PostAsync(journals, system, ctx)
    Yields JournalPostingResult[]
      ↓
[TmsBatcher] → [TmsSender]
```

The key difference from Approach B: **no per-system channels or Tasks**. The actor processes items
sequentially (or with configurable concurrency), selecting the right handler per item.

---

## Implementation Sketch

```csharp
/// <summary>
/// Generic ERP posting actor. Handles all system types by delegating to a typed handler.
/// The graph topology remains static; system-specific logic lives in handler implementations.
/// </summary>
public class ErpPostingActor : IStreamActor<SystemJournalProcessingItems, JournalPostingResult>
{
    private readonly IReadOnlyDictionary<string, IErpSystemHandler> _handlers;
    private readonly IErpSystemHandler _unroutedHandler;
    private readonly ILogger<ErpPostingActor> _logger;

    public ErpPostingActor(
        IEnumerable<INamedErpSystemHandler> handlers,
        UnroutedJournalHandler unroutedHandler,
        ILogger<ErpPostingActor> logger)
    {
        _handlers = handlers.ToDictionary(h => h.SystemName, h => (IErpSystemHandler)h);
        _unroutedHandler = unroutedHandler;
        _logger = logger;
    }

    public async IAsyncEnumerable<JournalPostingResult> ProcessAsync(
        IAsyncEnumerable<SystemJournalProcessingItems> input,
        IActorExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in input.WithCancellation(cancellationToken))
        {
            var systemName = item.System?.Name;
            var handler = systemName != null && _handlers.TryGetValue(systemName, out var h)
                ? h
                : _unroutedHandler;

            if (systemName != null && !_handlers.ContainsKey(systemName))
            {
                _logger.LogWarning("No ERP handler registered for system {System}. " +
                    "Routing to unrouted handler.", systemName);
            }

            await foreach (var result in handler.PostAsync(item, context, cancellationToken))
            {
                yield return result;
            }
        }
    }
}

/// <summary>
/// Named ERP handler that can be registered and discovered by system name.
/// </summary>
public interface INamedErpSystemHandler : IErpSystemHandler
{
    string SystemName { get; }
}

/// <summary>
/// SAP BTP connector implementation — registered once per SAP subscription type.
/// System-specific configuration comes from IOptionsSnapshot<SapConnectorAppSubscription>
/// keyed by SystemName, not injected per-instance.
/// </summary>
public class SapBtpErpHandler : INamedErpSystemHandler
{
    public string SystemName => "SAP";  // Or "SapS4Hana", per naming convention

    private readonly SapConnectorClient _sapClient;
    private readonly ISapPostRequestMapper _mapper;

    public SapBtpErpHandler(SapConnectorClient sapClient, ISapPostRequestMapper mapper)
    {
        _sapClient = sapClient;
        _mapper = mapper;
    }

    public async IAsyncEnumerable<JournalPostingResult> PostAsync(
        SystemJournalProcessingItems item,
        IActorExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // System-specific posting logic here
        foreach (var journal in item.Journals)
        {
            var request = _mapper.Map(journal);
            var response = await _sapClient.ODataPostAsync(request, cancellationToken);
            yield return new JournalPostingResult(journal, response);
        }
    }
}
```

### DI Registration

```csharp
// Register each ERP handler by system name — done at startup
services.AddScoped<INamedErpSystemHandler, SapBtpErpHandler>();
services.AddScoped<INamedErpSystemHandler, OracleErpHandler>();
// New system types added here when the library adds support for a new ERP integration
// (Not when a new tenant is added — tenants use IOptionsSnapshot for config)

services.AddScoped<UnroutedJournalHandler>();
services.AddScoped<ErpPostingActor>();

// Graph registration
df.AddActorBlock<SystemJournalProcessingItems, JournalPostingResult,
    ErpPostingActor>("erp-poster");
```

---

## Handling New Tenant Systems at Runtime

With this approach, "new system" means two distinct things:

1. **New ERP system type** (e.g., adding Oracle support for the first time):
   — Requires code change + deployment. No runtime handling needed.

2. **New tenant instance of an existing ERP type** (e.g., a customer adds a new SAP environment):
   — The `SapBtpErpHandler` already handles all SAP tenants via `IOptionsSnapshot`.
   — The `company-code → system` mapping is pre-loaded by the routing transformer.
   — No new blocks needed; just add the mapping record to the database.

This is often the **correct re-framing** of the problem. The legacy `AddPersistentRouter` created
new sub-flows per **system instance** (e.g., `SAP-Tenant1`, `SAP-Tenant2`), but the actual
business difference is just a different configuration value — the same SAP client code handles
both. The per-instance route was a consequence of how the legacy library worked, not a business
requirement.

---

## Concurrency

The `ErpPostingActor` processes items sequentially by default. To process different systems
concurrently, configure the block with `MaxConcurrency > 1` (competing consumer pattern):

```csharp
// Register N competing ErpPostingActor instances
df.AddActorBlock<SystemJournalProcessingItems, JournalPostingResult,
    ErpPostingActor>("erp-poster", maxConcurrency: 3);
```

Items are load-balanced across actor instances. For SAP, this reproduces the legacy `MaxConcurrency=3`
behaviour.

---

## Pros and Cons

| | |
|---|---|
| ✅ **Simplest to implement** | Minimal new code, no runtime channel creation |
| ✅ **Handles new tenants without restart** | Configuration-driven, not topology-driven |
| ✅ **Correct re-framing** | Often better matches the business requirement |
| ✅ **Graph-level observability** | Single block, metrics work as normal |
| ✅ **Extensible** | New ERP types added via DI registration |
| ❌ **Sequential within a batch** | No per-system concurrency isolation |
| ❌ **One slow system blocks others** | A slow SAP call holds up Oracle items in same worker |
| ❌ **Handler registration required** | Unknown system types fall to unrouted, not a new path |

### Mitigating the Sequential Bottleneck

If per-system isolation is required:

```csharp
// Upstream: group by system, then fan out with competing consumers
df.AddGraph("journal-flow", g =>
{
    g.UseBlock("routing-transformer")
     // Group so same-system items go to same worker (optional)
     .RouteBy(item => item.System?.Name ?? "unrouted")
     .To("erp-poster-sap",     key => key.StartsWith("SAP"))
     .To("erp-poster-oracle",  key => key.StartsWith("Oracle"))
     .To("erp-poster-other",   _ => true);  // Fallback / unrouted
});
```

This creates a few static routes by **ERP type** (not by tenant instance), which is typically
small and known at build time.

---

## When to Choose Approach C

- The set of **ERP integration types** (SAP, Oracle, etc.) is small and known at build time.
- New **tenants** of an existing type don't require a new sub-flow — only new configuration.
- You want the simplest possible migration that doesn't require new library features.
- This is the **recommended starting point** for most migrations from `AddPersistentRouter`.
