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

## Channel Stats / MonitoredChannel (Research Note)

`ChannelStatsEvent` exists in the event model and `ChannelState` is already tracked in
`FlowExecutionState`. The diagram already renders buffer utilisation labels between blocks.
However, nothing in the core runtime currently emits `ChannelStatsEvent`.

The core uses raw `Channel.CreateBounded<T>()` in several places (`EpochBufferBlock`,
`ReflectionHelper`, `TypedChannelFactory`). There is no wrapper that could hook into
the Blazor event sink.

A `MonitoredChannel<T>` wrapper concept would:
1. Wrap a `Channel.CreateBounded<T>()` with periodic sampling of `Reader.Count`
2. Emit `ChannelStatsEvent` through `IFlowEventSink` at a sampled interval
3. Be optional — when no sink is registered the overhead is zero

The design challenge is rate control: channel counts can change at item-processing frequency
which could be thousands of events per second. A reasonable approach would be to sample at
a fixed wall-clock interval (e.g. 500ms) rather than on every write, emitting at most one
`ChannelStatsEvent` per channel per interval. This keeps event volume bounded independent
of throughput.

This is deferred — it needs a separate design pass that covers how the channel factory
receives the sink reference. It is not part of the detail pane feature but is a natural
follow-on once the pane is in place (buffer utilisation heat-map on the diagram edges would
become much more useful).

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
| `Components/DetailPane.razor` | New — detail pane component |
| `Components/DetailPane.razor.css` | New — pane styles |
