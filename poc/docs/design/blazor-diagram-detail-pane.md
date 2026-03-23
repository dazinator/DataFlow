# Blazor Diagram — Detail Pane & UI Cleanup

## Context

The flow detail page (`FlowVisualization.razor`) currently has a flat layout: a status header on
top, then the trigger parameters block, then the SVG diagram. As more information becomes
available (block events, channel stats, trigger payload) the page becomes cluttered and there is
nowhere to drill into individual objects. This document captures the planned improvements.

---

## Current Issues

### 1. Block count shown in both views

`FlowRunsList` shows a **Blocks** column (line 53) and `FlowStatusPanel` shows a **Blocks**
metric (lines 34–36). The count tells you nothing useful — it's a static structural property
visible from the diagram itself. Remove from both surfaces.

**Files affected:**
- `FlowStatusPanel.razor` — remove the `Blocks` metric `<div>`
- `FlowRunsList.razor` — remove the `<th>Blocks</th>` column and the corresponding `<td>`

### 2. Items Processed always shows 0 — replace with Source Items Ingested

`FlowExecutionState.TotalItemsProcessed` sums `BlockState.ItemsProcessed` across all blocks.
`BlockState.ItemsProcessed` is updated only by `BlockProgressEvent`. Nothing in the core
DataFlow runtime currently emits `BlockProgressEvent` — the only items-processed tracking in
core goes through `DataFlowMetrics.ItemsProcessed()` which writes to an OpenTelemetry counter
(`dataflow.block.items.processed`), not to the Blazor event sink. So this field will always
read 0 in practice.

The sum-across-all-blocks concept is also semantically wrong: most blocks process the same
items as the blocks upstream of them (a transform block sees every item the source emitted),
so a naïve total double-counts. The correct flow-level total is the count of items fed *into*
the flow — i.e. the sum of items emitted by **source blocks** only (blocks with no incoming
edges). For the fan-in case this is exactly what you want:

```
producer-a  (100 items)  ──┐
                            ├──▶ buffer ──▶ batch ──▶ processor
producer-b  (50 items)   ──┘

Flow-level total = 150  (producer-a + producer-b, not 600 from summing all four blocks)
```

**Decision: replace `Items Processed` with `Source Items Ingested` at the flow level.**

#### What changes in the event model

`BlockStartedEvent` already carries `BlockType` (a string). Add a boolean `IsSource` flag:

```csharp
public record BlockStartedEvent(
    string BlockName,
    string BlockType,
    DateTime Timestamp,
    bool IsSource = false      // ← new, defaults false for backwards compat
) : IDataFlowEvent;
```

The core emitter (`DataFlowGraph.BlockRuntimeModel`) knows at `BlockStarted` time whether
the block has incoming edges (it is a graph property). It sets `IsSource = true` for blocks
where `GetIncomingEdges(block)` is empty. No new event type is needed.

`BlockProgressEvent` is reused unchanged — it is emitted for all blocks. The source/non-source
distinction is captured once, at `BlockStarted`, and stored in `BlockState.IsSource`.

#### What changes in state/display

- `BlockState` gains `bool IsSource { get; set; }`, set when `BlockStartedEvent` is processed.
- `FlowExecutionState.TotalSourceItemsIngested` = `Blocks.Values.Where(b => b.IsSource).Sum(b => b.ItemsProcessed)`
- `TotalItemsProcessed` is removed.
- `FlowStatusPanel` replaces "Items Processed" with "Items Ingested" sourced from
  `TotalSourceItemsIngested`. This metric is only meaningful once `BlockProgressEvent` is
  actually emitted for source blocks; until then it shows 0 and should be hidden (same
  conditional-on-nonzero rule as per-block display).
- Per-block "0 items" text in the diagram is still hidden when `ItemsProcessed == 0` (applies
  to all blocks, not just sources).

**Files affected:**
- `BlockEvents.cs` — add `IsSource = false` to `BlockStartedEvent`
- `BlockState` (in `FlowExecutionState.cs`) — add `IsSource` property
- `FlowExecutionState.cs` — replace `TotalItemsProcessed` with `TotalSourceItemsIngested`
- `EventProcessor.cs` — set `BlockState.IsSource` when processing `BlockStartedEvent`
- `FlowStatusPanel.razor` — rename label to "Items Ingested", hide when 0
- `FlowDiagram.razor` — conditionally render per-block items text: only if `ItemsProcessed > 0`

### 3. Trigger Parameters placement is poor

The trigger params collapsible card sits between the status header and the diagram, taking up
prime vertical space and visually breaking the flow. It should instead live in the detail pane
(see below), reachable via a small icon.

---

## New Feature: Detail Pane + Diagram Header Bar

### Layout after change

```
┌──────────────────────────────────────────────────────────┐
│  FlowStatusPanel  (unchanged except removals above)      │
├────────────┬─────────────────────────────────────────────┤
│            │ ⚙  [thin diagram header bar]                │
│            │                                             │
│  SVG       │         (detail pane — right side)          │
│  diagram   │                                             │
│  (flex-1)  │  shown when a block or trigger icon is      │
│            │  selected; hidden by default                │
└────────────┴─────────────────────────────────────────────┘
```

The diagram area becomes a horizontal flex row:
- **Left:** the SVG diagram (flex-grow: 1, same as today)
- **Right:** the detail pane (fixed width ~320px, hidden when nothing selected)

A thin header bar sits above the diagram flex row. It is not intended to stand out — its
purpose is just to provide placement for icons. Currently it will hold only the trigger icon.

### Diagram header bar

- Thin bar (e.g. 32px) with a light/neutral background
- Contains a **trigger icon** (e.g. a lightning bolt `⚡` or `⚙`) on the left
- Clicking it sets the selected object to `{ type: "trigger" }` and opens the detail pane
- If no `TriggerParamsJson` exists, the icon is not rendered

### Block selection

Add `@onclick` to each `<g class="block">` element in `FlowDiagram.razor`. Clicking a block
sets the selected object to `{ type: "block", blockName: "..." }` and opens the detail pane.
A selected block gets a subtle visual indicator (e.g. a slightly thicker or accent-coloured
border or a highlight ring around the SVG rect).

The selection state needs to propagate from `FlowDiagram` up to `FlowVisualization` (which
owns the layout), so the diagram needs an `EventCallback<SelectedObject?> OnSelectionChanged`
parameter.

### Detail pane component: `DetailPane.razor`

New component. Receives:

```csharp
[Parameter] public SelectedObject? Selection { get; set; }
[Parameter] public FlowExecutionState State { get; set; }
```

Where `SelectedObject` is a small discriminated union:

```csharp
public abstract record SelectedObject;
public record TriggerSelected : SelectedObject;
public record BlockSelected(string BlockName) : SelectedObject;
```

The pane renders nothing (or `display:none`) when `Selection` is null.

#### Trigger detail view

Shows the raw JSON of `State.TriggerParamsJson`, pretty-printed, with a **copy to clipboard**
button. Minimal styling — monospace pre block with a copy icon/button in the top-right corner.

#### Block detail view

Shows a small card for the selected block:

| Field | Source |
|-------|--------|
| Block name | `BlockState.BlockName` |
| Type | `BlockState.BlockType` |
| State | `BlockState.State` (with colour badge) |
| Start time | `BlockState.StartTime` |
| Duration | `BlockState.Duration` |
| Error | `BlockState.ErrorMessage` (only if failed) |

Below the summary, a **filtered event table** showing all events from
`FlowExecutionState.Events` where the event relates to this block name. Events to include:
`BlockStartedEvent`, `BlockCompletedEvent`, `BlockProgressEvent` (if ever emitted). Columns:
`Time`, `Event`, `Detail`.

This requires `FlowExecutionState` to retain the raw event log (not just the aggregated
state). See [Event log retention](#event-log-retention) below.

#### Custom block detail components via DynamicComponent

The default summary card is generic, but applications may want to render a richer, block-type
specific view (e.g. a source block showing current epoch, a batch block showing batch sizes).
Blazor's `DynamicComponent` supports this cleanly.

The library exposes a registration service:

```csharp
// Application startup
builder.Services.Configure<BlockDetailViewOptions>(opts =>
    opts.Register("EpochSourceBlock", typeof(MySourceDetailView)));
```

`DetailPane` checks the registry for the selected block's `BlockType`. If a matching
component type is found, it renders via `DynamicComponent` passing a `Parameters` dictionary
with at least `{ "BlockState", blockState }`. The custom component is expected to accept
a `[Parameter] BlockState BlockState` parameter. If no custom component is registered, the
default summary card is rendered as a fallback.

`DynamicComponent` is already in Blazor's standard library — no extra package needed.

---

## Event Log Retention

Currently `EventProcessor` discards event data after processing it into aggregated state.
To populate the block event table in the detail pane, we need to retain events.

Add a `List<IDataFlowEvent> EventLog` to `FlowExecutionState`. `EventProcessor.ProcessEvent`
appends every event to this list after processing it (ordering is guaranteed by the event
stream). The detail pane queries this list filtered by block name.

For the initial implementation, no pruning is needed — flows are short-lived in the demo and
the event count is small. If very long-running flows become a concern, a cap or rolling window
can be added later.

---

## Refactoring: Extract StreamPump from ReflectionHelper

Before adding the item counter, a prerequisite refactor is needed to give the counter a
clean home.

### The two concerns currently mixed in ReflectionHelper

`ReflectionHelper` currently contains two completely different things:

**1. Type bridge (reflection concern)** — public static methods that accept `Type` parameters
and use `MethodInfo.MakeGenericMethod()` and expression trees to dispatch to the correct
generic overload at runtime. This is unavoidable complexity: C# requires it when the type
parameter is only known at runtime. Examples: `EnumerateAndRouteTypedStreamAsync(object,
Type, ...)`, `CreateEpochStreamRoutingDelegate(Type)`, `GetTypedStreamFromChannelReader(...)`.

**2. Pump logic (execution concern)** — private generic methods that contain the actual
`await foreach` loops and routing dispatch. These have zero reflection dependency; they
could be in any class. Examples: `EnumerateAndRouteTypedStreamGenericAsync<T>`,
`EnumerateAndRouteEpochStreamAsync<TItem>`, `RouteItemToDownstreamChannelsAsync<TItem>`,
`MergeAsyncEnumerables<T>`, `CreateDownstreamEpochStreams<TItem>`.

The item counter, the periodic timer, and any future pump-level instrumentation all belong
in concern 2. Mixing them with concern 1 is what makes the code feel wrong.

### The name: StreamPump

The right term is **pump**. A pump actively drives items from a source to a sink — it pulls
from an `IAsyncEnumerable<T>` and pushes into downstream `ChannelWriter<T>` targets. This
is distinct from a *connector* (passive link) or a *router* (decides where items go —
`TypedEdgeRouter` already plays that role). The pump *drives the flow*.

The existing runtime model naming (`BlockRuntimeModel`, `EdgeRuntimeModel`,
`ExecutionPipeline`) naturally extends to `StreamPump` — it is the execution-time component
that runs between a block's output stream and the downstream channels.

Two pump variants map to the two code paths already in `ReflectionHelper`:
- **`StreamPump<T>`** — drives a plain `IAsyncEnumerable<T>` through routers into channel
  writers. The hot path: one atomic increment per item, fan-out via `Task.WhenAll`.
- **`EpochStreamPump<TItem>`** — wraps `StreamPump<TItem>` with epoch lifecycle management:
  creates per-epoch downstream `ChannelBackedEpochStream<TItem>` instances, routes the
  containers to downstream blocks before routing items, then completes/disposes them.

### Proposed file layout

```
DataFlow/Core/
  StreamPump.cs        ← new: all generic pump logic, no reflection
  ReflectionHelper.cs  ← reduced to type bridge only (dispatch to StreamPump<T>)
```

`ReflectionHelper`'s public methods remain identical in signature — they are the dispatch
layer that calls `StreamPump<T>` once the type is resolved. No call sites in
`DataFlowGraph.BlockRuntimeModel` change.

### Where the item counter lives

`StreamPump<T>` is an instance class (not static) because `BlockRuntimeModel` needs to
read the counter after (or during) execution:

```csharp
// Inside BlockRuntimeModel.RunAsync:
var pump = new StreamPump<T>(routers, epochStreamDelegate);
await pump.DriveAsync(typedOutput, cancellationToken);
// pump.ItemsEmitted is now the definitive count — emit BlockProgressEvent
```

The counter field is `long _itemsEmitted` incremented via `Interlocked.Increment` in the
`await foreach` loop. The periodic timer reads it via `Volatile.Read`. No allocations,
no lock contention.

`ReflectionHelper.EnumerateAndRouteTypedStreamAsync` becomes a thin wrapper that
instantiates the correct `StreamPump<T>` and calls `DriveAsync` — the same one-time
reflection cost it already has today.

### What stays in ReflectionHelper

Only methods that are *inherently* about type resolution:
- `CreateEpochStreamRoutingDelegate` / `CreateContainerRoutingDelegate` (expression tree compilation)
- `CreateTypedRouterFactory` (expression tree compilation)
- `CreateEmptyTypedStream` / `GetTypedStreamFromChannelReader` / `MergeTypedStreams` (MakeGenericMethod dispatch)
- `CompleteTypedWriter` (reflection invoke)
- The public `EnumerateAndRouteTypedStreamAsync` / `EnumerateTypedStreamAsync` entry points (dispatch only — delegate immediately to `StreamPump<T>`)

The private `*Generic*` and `EnumerateAndRoute*Async` methods, `EdgeRoutingTopology<TItem>`,
`CreateDownstreamEpochStreams`, `RouteEpochStreamContainersAsync`, and
`RouteItemToDownstreamChannelsAsync` all move to `StreamPump.cs`.

---

## BlockProgressEvent: Emission Strategy

Two questions: who counts, and how is the count delivered?

### Who counts (transparent infrastructure, not actors)

Adding `context.ReportProgress(n)` calls to individual actors would pollute actor code with
infrastructure concerns, be inconsistent across block types, and would need to be re-done
for every new block. The counting should be transparent.

With the `StreamPump` refactor above, the counter has a clean home: `StreamPump<T>` holds
a `long _itemsEmitted` field incremented via `Interlocked.Increment` in its `await foreach`
loop. This is the single place through which every block output item flows before being
written to downstream channels. ~1 ns per item: negligible alongside any real I/O.

`BlockRuntimeModel` creates the `StreamPump` instance, runs it, then owns the post-execution
counter value. It also already has `_incomingEdges` in scope at `BlockStarted` time, so
setting `IsSource` on `BlockStartedEvent` requires no new lookups.

### How the count is delivered (periodic batch, not per-item events)

Emitting a `BlockProgressEvent` for every single output item would flood the event stream
at the same rate as item throughput (potentially thousands/sec). Instead:

- `BlockRuntimeModel` starts a lightweight timer alongside the pump (e.g. every **500 ms**).
  The timer reads `pump.ItemsEmitted` via `Volatile.Read` and emits a `BlockProgressEvent`
  with the total so far. This bounds event volume to 2 events/block/second regardless of
  throughput.
- On `BlockCompleted`, the timer is stopped and one final `BlockProgressEvent` is emitted
  with the definitive count from `pump.ItemsEmitted`, ensuring the last partial window is
  never lost.

This is identical in spirit to how `DataFlowMetrics._activeChannelCount` uses an
`ObservableGauge` with a provider callback — the metric system samples on its own schedule,
not on the item schedule.

---

## Live Metrics: Event Stream vs. Polling API

There is a fundamental mismatch between two kinds of data:

| Kind | Examples | Natural model |
|------|----------|---------------|
| Discrete lifecycle events | block started/completed/failed, flow started | Event stream (SSE) |
| Continuously-changing state | buffer fill level, items-processed running total | Polled snapshot |

Forcing continuously-changing state through the event stream requires rate-limiting logic
in the emitter (the periodic-batch approach above), adds event volume to the SSE connection,
and makes the Blazor component replay a stream of deltas to reconstruct current state.
A polling endpoint returns current state directly.

### Buffer utilisation: polling over a metrics endpoint

Rather than routing `ChannelStatsEvent` through the event stream, a better fit is:

1. `MonitoredChannel<T>` (or the graph's channel factory) maintains per-channel `Reader.Count`
   snapshots in a concurrent dictionary keyed by channel name — updated on a 500 ms timer.
2. A lightweight `/api/flows/{invocationId}/channels` endpoint returns the current snapshot
   dictionary as JSON.
3. The Blazor diagram component polls this endpoint at its own preferred rate (e.g. 1 s),
   completely decoupled from how fast items move through channels.

Benefits: no rate-limiting complexity on the emission side; no channel stats in the
SSE stream; the diagram can adjust polling frequency based on whether it's visible; works
even if the SSE connection is slow.

The `ChannelStatsEvent` type and `ChannelState` in `FlowExecutionState` can remain as-is —
they would still be useful if channel stats are populated via the polling path (the Blazor
component writes them into `FlowExecutionState` after each poll response, so the diagram
rendering code does not change).

### BlockProgressEvent: keep in the event stream

Item counts are already bounded (periodic batch, max 2 events/block/sec) and they are
semantically log-like (monotonically increasing, part of the run history). Keeping them in
the event stream means the event log in `FlowExecutionState` captures the full progress
history for the detail pane's event table. The polling approach would only give current
state, losing the history.

### Summary

| Data | Delivery | Reason |
|------|----------|--------|
| Block lifecycle (started, completed, failed) | SSE event stream | Discrete, log-like |
| Item counts (`BlockProgressEvent`) | SSE event stream (rate-limited batch) | Log-like history useful; already bounded |
| Buffer utilisation | Polling API | Continuously changing current-value; streaming would require aggressive rate limiting |

---

## Channel Stats / MonitoredChannel (Research Note)

`ChannelStatsEvent` exists in the event model and `ChannelState` is already tracked in
`FlowExecutionState`. The diagram already renders buffer utilisation labels between blocks.
However, nothing in the core runtime currently emits `ChannelStatsEvent`.

The core uses raw `Channel.CreateBounded<T>()` in several places (`EpochBufferBlock`,
`ReflectionHelper`, `TypedChannelFactory`). There is no wrapper that could hook into
either the event sink or a polling snapshot store.

Based on the metrics architecture decision above, the preferred path is:

1. A `MonitoredChannel<T>` wrapper that maintains a current-depth snapshot updated on a
   500 ms timer (zero overhead on the read/write hot path).
2. The graph's channel registry exposes a snapshot method callable by the polling endpoint.
3. The polling endpoint is a thin controller returning the snapshot dictionary as JSON.
4. The Blazor component polls and writes results into `FlowExecutionState.ChannelStates`,
   which the diagram already reads for buffer labels.

This is deferred — it needs a separate design pass covering how the channel factory and
the polling endpoint share the snapshot registry. It is not part of the detail pane
feature but is a natural follow-on.

---

## Implementation Plan

Ordered by dependency:

**DataFlow core (prerequisite):**

0. **Extract `StreamPump`** from `ReflectionHelper` — move all generic pump logic to
   `DataFlow/Core/StreamPump.cs`; reduce `ReflectionHelper` to type-bridge only. No
   behaviour change, no call-site changes.

**Blazor cleanup (independent of each other):**

1. **Remove block count** from `FlowStatusPanel` and `FlowRunsList`
2. **Add `IsSource` to `BlockStartedEvent`**; set `BlockState.IsSource` in `EventProcessor`
3. **Replace `TotalItemsProcessed` with `TotalSourceItemsIngested`** in `FlowExecutionState`;
   rename/conditionalize metric in `FlowStatusPanel`; hide per-block "0 items" in `FlowDiagram`
4. **Remove trigger params card** from `FlowStatusPanel`

**BlockProgressEvent wiring (depends on step 0):**

5. **Add `long ItemsEmitted` counter to `StreamPump`**; increment via `Interlocked` in the
   pump loop
6. **Add periodic timer in `BlockRuntimeModel`** to emit `BlockProgressEvent` from
   `pump.ItemsEmitted`; emit final count at `BlockCompleted`

**Detail pane feature (depends on steps 2–4):**

7. **Add `EventLog` to `FlowExecutionState`**; append in `EventProcessor.ProcessEvent`
8. **Add `SelectedObject` discriminated union** (new small file in `Models/`)
9. **Add `DetailPane.razor`** with trigger view, default block card, and `DynamicComponent`
   fallback; add `BlockDetailViewOptions` registration service
10. **Add click handlers to `FlowDiagram`**; add `OnSelectionChanged` callback parameter;
    add visual selection highlight to the clicked block
11. **Add diagram header bar** with trigger icon to `FlowVisualization`
12. **Wire detail pane** into `FlowVisualization` layout (flex row with SVG + pane)

---

## Files Changed Summary

| File | Change |
|------|--------|
| `DataFlow/Core/StreamPump.cs` | New — all generic pump logic extracted from `ReflectionHelper`; `long ItemsEmitted` counter |
| `DataFlow/Core/ReflectionHelper.cs` | Reduced to type-bridge only; delegates to `StreamPump<T>` |
| `DataFlow/Core/DataFlowGraph.cs` | `BlockRuntimeModel`: set `IsSource` on `BlockStartedEvent`; add timer + emit `BlockProgressEvent` from `StreamPump.ItemsEmitted` |
| `FlowStatusPanel.razor` | Remove Blocks metric; rename Items Processed → Items Ingested (source only, hidden when 0); remove trigger params card |
| `FlowRunsList.razor` | Remove Blocks column |
| `BlockEvents.cs` | Add `IsSource = false` to `BlockStartedEvent` |
| `FlowExecutionState.cs` | Add `IsSource` to `BlockState`; replace `TotalItemsProcessed` with `TotalSourceItemsIngested`; add `EventLog` |
| `EventProcessor.cs` | Set `BlockState.IsSource` from event; append to `EventLog` |
| `FlowDiagram.razor` | Add `@onclick` on blocks, `OnSelectionChanged` callback, selection highlight, conditional items text |
| `FlowVisualization.razor` | Add header bar, flex layout, wire detail pane |
| `Models/SelectedObject.cs` | New — discriminated union |
| `Components/DetailPane.razor` | New — detail pane component; uses `DynamicComponent` for custom block views |
| `Components/DetailPane.razor.css` | New — pane styles |
| `Models/BlockDetailViewOptions.cs` | New — registration service for custom block detail component types |
