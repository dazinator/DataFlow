---
name: implement-dataflow-visualization
description: >
  Implement the Uniun DataFlow real-time visualization UI in an existing
  ASP.NET Core / Blazor WASM solution. Use this skill when asked to add
  flow monitoring, pipeline visualization, or real-time DataFlow dashboards
  to an application that uses the Uniun.DataFlow library.
---

# Implement DataFlow Visualization

You are implementing the **Uniun DataFlow Blazor visualization** into an existing
ASP.NET Core + Blazor WASM solution. This skill covers everything from package
installation through to a working, themed component on screen.

---

## Step 1 — Explore the codebase

**Do not make any changes yet.** Use `Glob` and `Read` to build a picture of
the solution. You are looking to answer:

- What is the hosting model? Hosted Blazor WASM (separate `.Client` / `.Server`
  projects), Blazor Server, or standalone WASM?
- Which `.csproj` is the ASP.NET Core host (contains `WebApplication.CreateBuilder`)?
- Which `.csproj` is the Blazor client (contains `WebAssemblyHostBuilder`)?
- Is there an existing `DbContext`? What is its name and EF Core provider
  (SQLite, SQL Server, PostgreSQL, other)?
- Does any project already reference `Uniun.DataFlow.*` packages? If so, which
  ones and what versions?
- Is there an existing nav menu, layout, or routing structure in the Blazor
  client that the new page should slot into?
- Is there an existing place in the codebase where DataFlow pipelines are
  executed (a service, worker, controller)? What does it look like?

---

## Step 2 — Present findings and confirm the plan

**Stop and talk to the user before writing any code.**

Summarise what you found in Step 1 in a short, readable form, then present a
proposed implementation plan. Flag any decisions that need the user's input.
Do not proceed until the user confirms.

Example structure for your message:

---
**What I found**

| | |
|---|---|
| Hosting model | Hosted Blazor WASM (`MyApp.Server` + `MyApp.Client`) |
| Existing DbContext | `AppDbContext` (SQL Server) in `MyApp.Server` |
| DataFlow packages | `Uniun.DataFlow` 2.1.0 already referenced in `MyApp.Server` |
| Pipeline execution | `InvoiceProcessingService.cs` — injected into a background worker |
| Nav / routing | `NavMenu.razor` present; routes defined per-page with `@page` |

**Proposed plan**

1. Add `Uniun.DataFlow.Blazor.Server` to `MyApp.Server`
2. Add `Uniun.DataFlow.Blazor` to `MyApp.Client`
3. Use **Option B** (BYO DbContext) — merge entities into `AppDbContext`
4. Wire `IFlowEventSink` into `InvoiceProcessingService`
5. Add a `/flow-monitor/{invocationId}` page to `MyApp.Client`
6. Add a nav link to the monitor page in `NavMenu.razor`

**Questions before I start**

- For the monitoring page route, is `/flow-monitor/{id}` appropriate, or would
  you prefer a different path?
- Should the nav link be visible to all users, or is there an auth policy I
  should apply?
- Any preference on where in `NavMenu.razor` the link should appear?
---

Adapt the questions to what you actually found. If everything is unambiguous,
you can reduce the questions — but always show the plan and wait for a
go-ahead before making file changes.

---

## Step 3 — Install NuGet packages

### Server / host project

```bash
dotnet add <ServerProject>.csproj package Uniun.DataFlow.Blazor.Server
```

This pulls in `Uniun.DataFlow.Blazor.Shared` transitively. It provides:
- `IFlowEventSink` — receives events from the DataFlow engine
- `EfCoreFlowEventSink<TContext>` — EF Core persistence implementation
- `FlowEventsHub<TContext>` — SignalR hub for live streaming to the client
- `FlowVisualizationDbContext` — minimal schema for apps without an existing DbContext
- `AddDataFlowVisualizationServer()` / `MapDataFlowEndpoints()` extension methods
- `DataFlowModelBuilderExtensions.AddDataFlowVisualizationEntities()` — for merging into an existing DbContext

### Blazor WASM client project

```bash
dotnet add <ClientProject>.csproj package Uniun.DataFlow.Blazor
```

This provides:
- `<FlowVisualization>` — root live-visualization component, drop onto any page
- `<FlowRunsList>` — ready-made table listing all flow runs with status and links
- `IEventSource` / `HttpSignalREventSource` — client-side event streaming
- `AddDataFlowVisualizationClient()` extension method

---

## Step 4 — Server-side wiring (`Program.cs` of the ASP.NET Core host)

### Option A — dedicated DbContext (no existing EF setup)

Add the following **before** `builder.Build()`:

```csharp
// Choose your EF Core provider.
// SQLite — good for dev / lightweight deployments:
builder.Services.AddDataFlowVisualizationServer(options =>
    options.UseSqlite("Data Source=dataflow-viz.db"));

// SQL Server:
builder.Services.AddDataFlowVisualizationServer(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DataFlow")));

// PostgreSQL:
builder.Services.AddDataFlowVisualizationServer(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DataFlow")));
```

An optional second parameter `periodicSnapshotInterval` (default `100`) controls
how often a state snapshot is materialised during long-running flows. Pass `0` to
disable periodic snapshots.

Add the following **after** `builder.Build()`:

```csharp
// Maps HTTP endpoints (GET /flows, GET /flows/{id}/state) and the SignalR hub
app.MapDataFlowEndpoints();
```

Optionally, ensure the schema is created on first run:

```csharp
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider
         .GetRequiredService<FlowVisualizationDbContext>()
         .Database.EnsureCreated();
```

> **Migrations note**: `EnsureCreated()` is fine for development and SQLite.
> For production SQL Server / PostgreSQL, generate an EF migration:
> `dotnet ef migrations add AddDataFlowVisualization --context FlowVisualizationDbContext`

### Option B — bring your own DbContext (app already has EF Core)

First, call the entity configuration extension in your DbContext's `OnModelCreating`:

```csharp
public class MyAppDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddDataFlowVisualizationEntities(); // registers the two tables
        // ... rest of your model config ...
    }
}
```

Then in `Program.cs`, before `builder.Build()`:

```csharp
// Your existing DbContext registration — unchanged
builder.Services.AddDbContext<MyAppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Point DataFlow at your context — no second DbContext or connection string needed
builder.Services.AddDataFlowVisualizationServer<MyAppDbContext>();
```

And after `builder.Build()`:

```csharp
app.MapDataFlowEndpoints<MyAppDbContext>();
```

Generate a migration as you normally would for your context:

```bash
dotnet ef migrations add AddDataFlowVisualization --context MyAppDbContext
dotnet ef database update --context MyAppDbContext
```

---

## Step 5 — Client-side DI registration (`Program.cs` of the Blazor WASM project)

```csharp
// Connects to the SignalR hub and the HTTP catch-up endpoint on the host.
// hubPath must match the path passed to MapDataFlowEndpoints() (default: /hubs/flow-events)
builder.Services.AddDataFlowVisualizationClient(
    baseUrl: builder.HostEnvironment.BaseAddress,
    hubPath: "/hubs/flow-events");
```

If the solution does **not** yet have an `HttpClient` registered (rare for WASM but check):

```csharp
builder.Services.AddScoped(sp =>
    new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
```

---

## Step 6 — CSS setup in the host HTML

Locate the host HTML file — `wwwroot/index.html` for hosted WASM,
`Components/App.razor` for Blazor Server — and add both `<link>` tags inside
`<head>`:

```html
<!-- 1. Scoped component styles (generated by Blazor build toolchain) -->
<link rel="stylesheet"
      href="_content/Uniun.DataFlow.Blazor/Uniun.DataFlow.Blazor.styles.css" />

<!-- 2. Default theme variables (required for colours, fonts, radii) -->
<link rel="stylesheet"
      href="_content/Uniun.DataFlow.Blazor/dataflow-blazor.css" />
```

Load these **before** the application's own stylesheet so any custom overrides
in `app.css` take precedence.

For a complete reference of every themeable CSS variable, see
[`docs/guides/blazor-theming.md`](../../../docs/guides/blazor-theming.md).

---

## Step 7 — Add `@using` to `_Imports.razor`

In the Blazor client project's `_Imports.razor`:

```razor
@using DataFlow.Blazor.Components
@using DataFlow.Blazor.Events
```

---

## Step 8 — Drop the components onto pages

### Live visualization for a single run

Create a monitoring page or add to an existing one:

```razor
@page "/flow-monitor/{InvocationId:guid}"
@using DataFlow.Blazor.Components

<FlowVisualization InvocationId="@InvocationId" />

@code {
    [Parameter]
    public Guid InvocationId { get; set; }
}
```

### All-runs history table (optional)

`<FlowRunsList />` is a self-contained component with no required parameters.
It polls `GET /flows` every few seconds and renders a table of all runs with
status, duration, and a "View →" link that navigates to each run's detail page.

```razor
@page "/flows"
@using DataFlow.Blazor.Components

<h1>Flow Runs</h1>
<FlowRunsList />
```

---

## Step 9 — Wire up `IFlowEventSink` in the DataFlow execution code

The visualization becomes live once the DataFlow engine emits events.
`IFlowEventSink` is registered in the DI container by
`AddDataFlowVisualizationServer()`. The engine picks it up automatically if
the `IExecutionContext.ServiceProvider` resolves from the same container.

If the pipeline is constructed manually, resolve and pass the emitter:

```csharp
// In a service / controller / worker that runs the DataFlow graph:
public class PipelineRunner
{
    private readonly IFlowEventSink _sink;

    public PipelineRunner(IFlowEventSink sink)
    {
        _sink = sink;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var invocationId = Guid.NewGuid();
        var emitter = new BoundFlowEventEmitter(_sink, invocationId);

        await emitter.EmitAsync(new FlowStartedEvent(
            invocationId, "My Pipeline", DateTime.UtcNow,
            TriggerParamsJson: null));       // see security note below

        await emitter.EmitAsync(new BlockStartedEvent(
            "extract", "HttpSourceBlock", DateTime.UtcNow,
            IsSource: true));               // IsSource=true for blocks with no input edge

        // Periodic progress — emitted every ~500 ms during execution:
        await emitter.EmitAsync(new BlockMetricsEvent(
            "extract", ItemsConsumed: 0, ItemsProduced: itemsEmitted, DateTime.UtcNow));

        // Buffer health — emit whenever a channel's count changes:
        await emitter.EmitAsync(new ChannelStatsEvent(
            "extract", "transform", BufferCapacity: 100, CurrentCount: 12, DateTime.UtcNow));

        // Edge throughput — emitted in parallel with BlockMetricsEvent:
        await emitter.EmitAsync(new EdgeProgressEvent(
            "extract", "transform", ItemsTransmitted: itemsEmitted, DateTime.UtcNow));

        await emitter.EmitAsync(new BlockCompletedEvent(
            "extract", success: true, DateTime.UtcNow));

        await emitter.EmitAsync(new FlowCompletedEvent(
            invocationId, success: true, DateTime.UtcNow));

        // Navigate the user (or provide a link) to:
        //   /flow-monitor/{invocationId}
    }
}
```

### Event reference

| Event | When to emit |
|---|---|
| `FlowStartedEvent(InvocationId, FlowName, Timestamp, TriggerParamsJson?, CorrelationId?, AttemptNumber)` | Once, at flow start |
| `BlockStartedEvent(BlockName, BlockType, Timestamp, IsSource)` | Once per block when it begins executing. Set `IsSource = true` for source blocks (no input edge) — used to compute "Items Ingested" in the header. |
| `BlockMetricsEvent(BlockName, ItemsConsumed, ItemsProduced, Timestamp)` | Periodically (~500 ms) and on completion. Cumulative totals, not deltas. `ItemsConsumed = 0` for source blocks; `ItemsProduced = 0` for pure sinks. |
| `ChannelStatsEvent(SourceBlock, TargetBlock, BufferCapacity, CurrentCount, Timestamp)` | Periodically per edge. Drives the buffer health pill (green/amber/red) shown on each connection. |
| `EdgeProgressEvent(SourceBlock, TargetBlock, ItemsTransmitted, Timestamp)` | Periodically per edge. Cumulative count — used to compute edge throughput rate. |
| `BlockCompletedEvent(BlockName, Success, Timestamp, ErrorMessage?)` | Once per block when it finishes (success or failure). `ErrorMessage` is shown in the detail pane on failure. |
| `FlowCompletedEvent(InvocationId, Success, Timestamp, ErrorMessage?)` | Once, when the entire flow finishes. |

### Retry / correlation support

`FlowStartedEvent` accepts optional `CorrelationId` (a stable ID shared across
all retry attempts for the same logical work item) and `AttemptNumber` (1-based).
When provided, `<FlowRunsList />` groups retries under the same correlation and
shows attempt badges.

> **Security note**: `TriggerParamsJson` on `FlowStartedEvent` is stored
> verbatim and sent to all connected browser clients. Never include secrets,
> API keys, or PII in trigger parameters.

---

## Step 10 — Optional theming

To match the application's colour scheme, override CSS variables in the app's
own stylesheet (after the library stylesheets):

```css
/* wwwroot/app.css */
:root {
    --df-panel-header-from: #1a1a2e;
    --df-panel-header-to:   #16213e;
    --df-radius:            4px;
}
```

Full variable reference: [`docs/guides/blazor-theming.md`](../../../docs/guides/blazor-theming.md).

---

## Checklist before finishing

Read through each item and verify it is done, or note why it doesn't apply:

- [ ] `Uniun.DataFlow.Blazor.Server` added to server project
- [ ] `Uniun.DataFlow.Blazor` added to Blazor client project
- [ ] `AddDataFlowVisualizationServer(…)` (or `AddDataFlowVisualizationServer<TContext>()`) in server `Program.cs`
- [ ] `app.MapDataFlowEndpoints()` (or `app.MapDataFlowEndpoints<TContext>()`) in server `Program.cs`
- [ ] If BYO-context: `modelBuilder.AddDataFlowVisualizationEntities()` called in `OnModelCreating`
- [ ] EF schema creation/migration applied
- [ ] `AddDataFlowVisualizationClient(baseUrl: …)` in client `Program.cs`
- [ ] Both `<link>` tags in host HTML
- [ ] `@using` directives in `_Imports.razor`
- [ ] `<FlowVisualization InvocationId="…" />` on a routable page
- [ ] `IFlowEventSink` / `BoundFlowEventEmitter` wired into the pipeline execution code
- [ ] Solution builds (`dotnet build`)
- [ ] Navigate to the monitor page in a browser and verify the visualization loads

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Component renders but has no styling | Missing `<link>` tags in host HTML |
| Colours are default but theming overrides not working | App stylesheet loaded **before** library stylesheet — swap order |
| SignalR connection refused | `hubPath` in `MapDataFlowEndpoints` doesn't match `hubPath` in `AddDataFlowVisualizationClient` |
| BYO-context: EF can't find the tables | `modelBuilder.AddDataFlowVisualizationEntities()` not called in `OnModelCreating`, or migration not applied |
| "Loading flow visualization…" never resolves | `IEventSource` not registered, or server endpoints not mapped |
| EF exception on first run | EF provider package not installed, or `EnsureCreated()` not called |
| No events appear despite the pipeline running | `IFlowEventSink` not resolved from `IExecutionContext.ServiceProvider` — check DI wiring |
| Flow stuck showing RUNNING after completion | `FlowCompletedEvent` not emitted — ensure it is always sent, even on exception paths |
| Buffer health pills absent | `ChannelStatsEvent` not being emitted — check edge event wiring |
| Block stat counts not showing | `BlockMetricsEvent` not emitted, or `ItemsConsumed`/`ItemsProduced` both zero |

---

## Reference

- Component README: `poc/DataFlow.Blazor/README.md`
- Theming guide:   `docs/guides/blazor-theming.md`
- Server extension:`DataFlow.Blazor.Server/Extensions/DataFlowVisualizationServerExtensions.cs`
- Client extension:`DataFlow.Blazor/Extensions/DataFlowVisualizationClientExtensions.cs`
- Event model:     `DataFlow.Blazor.Shared/Events/`
