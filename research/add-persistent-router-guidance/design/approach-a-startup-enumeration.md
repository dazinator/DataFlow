# Approach A: Pre-Enumerate Routes at Startup

**Category**: Static-topology, known-bounded route set  
**Effort**: Low  
**POC library changes required**: None

---

## Overview

When the full set of route destinations (e.g. ERP system instances) is **bounded and knowable at
startup**, enumerate them from configuration or the database during DI registration and create a
static route for each using `SelectiveRoutingEdgeStrategy<T>`.

A "catch-all" or "unrouted" route handles any system not in the known set — which in practice
means either a misconfiguration or a system added after startup (triggering a restart).

---

## How it Works

```
[Routing Transformer]
  Emits SystemJournalProcessingItems (tagged with System.Name)
      ↓
[SelectiveRoutingEdgeStrategy<SystemJournalProcessingItems>]
  ├── "unrouted"    → [UnroutedJournalTransformer] → [TmsBatcher] → [TmsSender]
  ├── "SAP-Tenant1" → [SapSender(SAP-Tenant1)]     → [TmsBatcher] → [TmsSender]
  ├── "SAP-Tenant2" → [SapSender(SAP-Tenant2)]     → [TmsBatcher] → [TmsSender]
  └── "Oracle-T1"  → [OracleSender(Oracle-T1)]    → [TmsBatcher] → [TmsSender]
```

---

## DI Registration Example

```csharp
// In IDataFlowConfiguration.Configure() or wherever the graph is built:
builder.Services.AddDataFlows("journal-processing", df =>
{
    // Resolve known ERP systems from configuration or database at startup
    var erpSystems = serviceProvider.GetRequiredService<IErpSystemRepository>()
        .GetAllSystemsAsync().GetAwaiter().GetResult(); // acceptable at startup

    // Always add the "unrouted" route
    df.AddActorBlock<SystemJournalProcessingItems, JournalPostingResult,
        UnroutedJournalPostingResultTransformer>("unrouted-handler");

    // Register one block per known system
    foreach (var system in erpSystems)
    {
        // Key services by system name so each block gets the right configuration
        var systemKey = system.Name;
        df.AddActorBlock<SystemJournalProcessingItems, JournalPostingResult,
            SapBtpConnectorErpJournalSender>($"erp-handler-{systemKey}");
    }

    // Shared TMS blocks (can be a broadcast fan-in via merge, see note)
    df.AddBatchBlock<JournalPostingResult>("tms-batcher", maxBatchSize: 200,
        windowPeriod: TimeSpan.FromSeconds(2));
    df.AddActorBlock<JournalPostingResult[], Unit, TmsJournalStatusSender>("tms-sender");

    df.AddGraph("journal-flow", g =>
    {
        g.UseBlock("producer")
         .BatchWith("journal-batcher")
         .RateLimitWith("rate-limiter")
         .ProcessWith("routing-transformer")
         .RouteBy(item => item.System?.Name ?? "unrouted")
         .To("unrouted-handler", key => key == "unrouted")
         .To("erp-handler-SAP-Tenant1", key => key == "SAP-Tenant1")
         .To("erp-handler-SAP-Tenant2", key => key == "SAP-Tenant2")
         // ... one .To() per known system
         ;

        // Each route path continues to TMS blocks
        // (this requires merge or per-route TMS blocks — see Merge note below)
    });
});
```

### Notes on TMS Fan-In (Merge)

Because each route emits `JournalPostingResult` items that all need to pass through the TMS
status sender, you need to **merge** the outputs before the shared TMS blocks:

- **Option 1 — Per-route TMS blocks**: Duplicate the `TmsBatcher → TmsSender` pair for each
  route. Simple but has N×2 blocks.
- **Option 2 — Merge block**: Use a future `MergeBlock<T>` (not yet in POC) to fan in from all
  routes into a single batcher. This is the cleaner architecture when available.
- **Option 3 — Competing consumer into shared channel**: All per-route outputs write to a single
  shared `BufferBlock<JournalPostingResult>` which then feeds the common TMS path.

---

## Handling New Systems at Runtime

When a new ERP tenant is added to the database while the application is running:

1. The routing transformer encounters an unknown system name.
2. **Two options**:
   a. **Treat as "unrouted"** (log a warning, journals flow through unrouted path).
   b. **Trigger a restart** of the data flow host on detection of an unknown system.
3. After restart, DI registration re-enumerates systems and the new route is available.

This is acceptable in most cases because:
- Adding an ERP tenant is an infrequent administrative action.
- A controlled restart is safe compared to truly dynamic graph mutation.

---

## Pros and Cons

| | |
|---|---|
| ✅ **Zero new library code** | Uses existing `SelectiveRoutingEdgeStrategy<T>` |
| ✅ **Full graph observability** | All routes visible in graph visualization |
| ✅ **Simple to reason about** | Static topology, no runtime mutation |
| ✅ **Correct shutdown semantics** | All channels flushed cleanly |
| ❌ **Requires restart for new systems** | Cannot add routes without re-registration |
| ❌ **Unknown systems fall through** | Must handle unknown route key explicitly |
| ❌ **Scales poorly for many systems** | 100+ ERP instances = 100+ blocks |

---

## When to Choose Approach A

- The set of route destinations is **small** (< 20 systems).
- Adding a new system is an **infrequent administrative action** that tolerates a restart.
- You need **full graph-level observability** for all routes.
- You want the **simplest possible migration** path from the legacy flow.

---

## Key Code Reference

`SelectiveRoutingEdgeStrategy<TItem>` — already implemented in:
```
/poc/DataFlow/Core/SelectiveRoutingEdgeStrategy.cs
```
