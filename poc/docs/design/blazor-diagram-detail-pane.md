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

## BlockProgressEvent: Emission Strategy

Two questions: who counts, and how is the count delivered?

### Who counts (transparent infrastructure, not actors)

Adding `context.ReportProgress(n)` calls to individual actors would pollute actor code with
infrastructure concerns, be inconsistent across block types, and would need to be re-done
for every new block. The counting should be transparent.

`DataFlowGraph.BlockRuntimeModel` is the right place. It already owns the per-block lifecycle
(`BlockStartedEvent`, `BlockCompletedEvent`) and calls `ReflectionHelper.EnumerateAndRouteTypedStreamAsync`
— the single choke point through which all block output flows before being written to
downstream channels. Adding an `Interlocked.Increment` on a `long` counter per output item
here is ~1 ns per item: negligible alongside any real I/O or computation.

`BlockRuntimeModel` also already has `_incomingEdges` in scope, so it can set `IsSource`
on `BlockStartedEvent` without any new lookups (a block is a source when `_incomingEdges`
for it is empty).

### How the count is delivered (periodic batch, not per-item events)

Emitting a `BlockProgressEvent` for every single output item would flood the event stream
at the same rate as item throughput (potentially thousands/sec). Instead:

- `BlockRuntimeModel` increments a `long _itemsEmitted` field (via `Interlocked.Increment`)
  in the enumeration loop — zero allocations, minimal cost.
- A lightweight timer fires every **500 ms** (configurable). It snapshots the current counter
  and emits a `BlockProgressEvent` with the delta since the last snapshot. This bounds
  event volume to 2 events/block/second regardless of throughput.
- On `BlockCompleted`, one final `BlockProgressEvent` is emitted with the definitive total,
  ensuring the last partial window is never lost.

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

1. **Remove block count** from `FlowStatusPanel` and `FlowRunsList`
2. **Add `IsSource` to `BlockStartedEvent`**; set `BlockState.IsSource` in `EventProcessor`
3. **Replace `TotalItemsProcessed` with `TotalSourceItemsIngested`** in `FlowExecutionState`;
   rename/conditionalize metric in `FlowStatusPanel`; hide per-block "0 items" in `FlowDiagram`
4. **Remove trigger params card** from `FlowStatusPanel`
5. **Add `EventLog` to `FlowExecutionState`**; append in `EventProcessor.ProcessEvent`
6. **Add `SelectedObject` discriminated union** (new small file in `Models/`)
7. **Add `DetailPane.razor`** with trigger view and block view sub-components
8. **Add click handlers to `FlowDiagram`**; add `OnSelectionChanged` callback parameter;
   add visual selection highlight to the clicked block
9. **Add diagram header bar** with trigger icon to `FlowVisualization`
10. **Wire detail pane** into `FlowVisualization` layout (flex row with SVG + pane)

Steps 1–4 are independent cleanup and event model changes. Steps 5–10 are the new feature
and should be done in order.

---

## Files Changed Summary

| File | Change |
|------|--------|
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
