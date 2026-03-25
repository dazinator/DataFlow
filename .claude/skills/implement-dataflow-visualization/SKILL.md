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

## Package model — read this first

`Uniun.DataFlow.Blazor.Server` and `Uniun.DataFlow.Blazor` are **new, separate
visualization packages** added alongside the existing core `Uniun.DataFlow`
package. They do **not** replace or require an upgrade of the core library.
Do not bump the version of any existing `Uniun.DataFlow.*` references — just
add the two new packages.

## DataFlow API versions — legacy vs. V2

Some codebases contain two generations of DataFlow usage:

- **Legacy API** — `IDataFlowConfiguration`, `DataFlowBuilder`, `FlowExecutor<T>`.
  Pipeline topology is implicit (defined inside individual block classes).
- **V2 / current API** — `DataFlowGraph`, `AddDataFlows(…)`, explicit `.Connect(from, to)`
  calls that fully describe the topology.

**The visualization targets the V2 API.** If only the legacy API exists, the
visualization can still be wired by emitting events manually, but the automatic
topology rendering will not be available. If both exist, focus on the V2 flows.

No changes to the existing pipeline topology (`.Connect()` calls, block
registrations, etc.) are needed. The visualization is an **event emitter** that
sits alongside the existing execution code — it observes what happens without
modifying how the pipeline runs.

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
  ones and what versions? (**Do not plan to change those versions.**)
- Does the codebase use the **legacy API** (`IDataFlowConfiguration`,
  `DataFlowBuilder`, `FlowExecutor<T>`), the **V2 API** (`DataFlowGraph`,
  `AddDataFlows`, `.Connect()`), or both? The visualization targets V2.
- Is there an existing nav menu, layout, or routing structure in the Blazor
  client that the new page should slot into?
- Is there an existing place in the codebase where DataFlow pipelines are
  registered and executed (a service, worker, controller, registration class)?
  What does it look like? This is where `IFlowEventSink` will be wired in.

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
| DataFlow packages | `Uniun.DataFlow` 2.1.0 already referenced in `MyApp.Server` (not changing this version) |
| DataFlow API version | V2 (`DataFlowGraph` / `.Connect()`) used in `JournalProcessingRegistration.cs` |
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

> **Required usings for this step:**
> ```csharp
> using DataFlow.Blazor.Server;                    // AddDataFlowVisualizationServer, MapDataFlowEndpoints
> using DataFlow.Blazor.Server.Persistence;        // FlowVisualizationDbContext (Option A), DataFlowModelBuilderExtensions (Option B)
> ```

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
using DataFlow.Blazor.Server.Persistence; // required for AddDataFlowVisualizationEntities

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

> **Hub ownership**: if you want full control over how the hub is registered
> (custom path, auth policies, Azure SignalR options) use
> `MapDataFlowHttpEndpoints` instead and map the hub yourself — see
> [Hub registration](#hub-registration) below.

Generate a migration as you normally would for your context:

```bash
dotnet ef migrations add AddDataFlowVisualization --context MyAppDbContext
dotnet ef database update --context MyAppDbContext
```

### Multi-tenant apps — adding a TenantId shadow property

The DataFlow entities do **not** include a `TenantId` CLR property. This is intentional: different applications use different types for their tenant identifier (`int`, `Guid`, `string`, etc.). Including a concrete CLR property would cause an EF Core type-mismatch error when the app's tenant model uses a different type.

Instead, add tenant isolation **after** calling `AddDataFlowVisualizationEntities()` using EF Core [shadow properties](https://learn.microsoft.com/en-us/ef/core/modeling/shadow-properties). For example, if your app has a helper that adds an `int` shadow property and a global query filter:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Register the DataFlow tables first
    modelBuilder.AddDataFlowVisualizationEntities();

    // Then add your own tenant filter — works with any type (int, Guid, string, …)
    modelBuilder.HasTenantIdFilter<FlowEventRecord>(tenantId);
    modelBuilder.HasTenantIdFilter<FlowSnapshotRecord>(tenantId);
}
```

Because `TenantId` is not a CLR property on these entities, EF Core treats it as a pure shadow property and there is no type conflict.



## Hub registration

`MapDataFlowEndpoints` registers the hub for you as a convenience. If the
application needs full control — custom path, auth policy, or Azure SignalR
Service options — use `MapDataFlowHttpEndpoints` for the HTTP endpoints and
map the hub separately:

> **Important**: `MapDataFlowEndpoints()` returns `IEndpointRouteBuilder`, **not**
> `IEndpointConventionBuilder`. You **cannot** chain `.RequireAuthorization()` directly
> on it — this causes CS0311. If you need auth, use the split approach below and
> call `.RequireAuthorization()` on the individual `MapHub` call instead.

### Server (`Program.cs`)

```csharp
// HTTP endpoints only — no hub
app.MapDataFlowHttpEndpoints<MyAppDbContext>();   // BYO-context variant
// (or app.MapDataFlowHttpEndpoints() for the dedicated-context variant)

// App owns the hub mapping — apply whatever options are needed
app.MapHub<FlowEventsHub<MyAppDbContext>>("/my/hub/path")
   .RequireAuthorization();  // RequireAuthorization is valid here — MapHub returns IHubEndpointConventionBuilder
```

`FlowEventsHub<TContext>` is a plain `Hub` subclass and is transport-agnostic.
It works identically with local SignalR and Azure SignalR Service — no extra
configuration is needed; Azure SignalR intercepts the transport layer
transparently.

### Client (`Program.cs`)

```csharp
builder.Services.AddDataFlowVisualizationClient(
    baseUrl: builder.HostEnvironment.BaseAddress,
    hubPath: "/my/hub/path");   // must match the path used in MapHub above
```

---

## Step 5 — Client-side DI registration (`Program.cs` of the Blazor WASM project)

> **Required using for this step:**
> ```csharp
> using DataFlow.Blazor.Extensions; // AddDataFlowVisualizationClient
> ```

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
      href="_content/Uniun.DataFlow.Blazor/Uniun.DataFlow.Blazor.bundle.scp.css" />

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

By default the "View →" button navigates to `/flows/{guid}`. To use a different
path, pass `ViewUrlTemplate` with `{0}` as the GUID placeholder:

```razor
<FlowRunsList ViewUrlTemplate="/apps/JNL/DataFlow/Runs/{0}" />
```

The monitoring page you create (Step 8, live visualization) must be routable at
whatever path you choose here, e.g. `@page "/apps/JNL/DataFlow/Runs/{InvocationId:guid}"`.

---

## Step 9 — Wire up `IFlowEventSink` in the DataFlow execution code

> **What this step is — and isn't**: Wiring `IFlowEventSink` means adding
> event-emission calls to the code that **runs** the pipeline. It does **not**
> mean changing how the pipeline is defined — the existing `DataFlowGraph`,
> `.Connect()` calls, block registrations, and DI setup all stay exactly as-is.
> You are only adding a "side-channel" observer that reports what is happening.
>
> If the app uses the V2 API with a named graph (e.g.
> `AddDataFlows("my-graph", df => { … })`) find the service or worker that
> invokes that graph and add emission there. If the graph is invoked via a
> framework entry point (e.g. a hosted service calling `IDataFlowRunner`), inject
> `IFlowEventSink` into that class.

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
| `FlowGraphDefinedEvent(Blocks, Edges, Timestamp)` | Once, immediately after the pipeline is built but before any block starts. Carries the complete static topology (block names, types, item labels, edge wiring). The client uses this to render the full diagram in a single frame rather than block-by-block. **Not** an audit event — excluded from the History tab. |
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

## Step 9b — Optional: configure block display names and type labels

By default the diagram shows:
- **Block title** — the block's registered name (e.g. `journal-v2:erp-poster-1`)
- **Block type** — a friendly default for native epoch block types (`EpochActorBlock`3`` → "Actor", `EpochBufferBlock`1`` → "Buffer", etc.). Unknown types fall back to `Type.Name` unchanged.

If the registered name is long or technical, or if a non-native block type needs a friendlier label, configure block metadata inline at registration time using the optional callback on `AddActorBlock` / `AddBlock` / `AddBatch` / `AddRateLimit` / `AddSourceBlock` / `AddEpochBuffer`:

```csharp
services.AddDataFlows("journal-v2", df =>
{
    // DisplayName overrides the title shown in the diagram for this block instance.
    // TypeLabel overrides the type label beneath the title (optional — native types
    // already have sensible defaults).
    df.AddActorBlock<Invoice, PostedInvoice, ErpPosterActor>("erp-poster-1", meta =>
    {
        meta.DisplayName("ERP Poster");
    });

    df.AddActorBlock<Invoice, PostedInvoice, CustomRouter>("custom-router", meta =>
    {
        meta.DisplayName("Custom Router")
            .TypeLabel("Router");   // override for non-native block type
    });

    df.AddBatch<Invoice>("batcher", maxBatchSize: 100, meta: meta =>
    {
        meta.DisplayName("Batch");
    });

    df.AddRateLimit<Invoice[]>("rate-limiter", permitLimit: 5, window: TimeSpan.FromSeconds(1), meta: meta =>
    {
        meta.DisplayName("Rate Limit");
    });

    df.AddEpochBuffer<Invoice[]>("erp-buffer", capacity: 50, meta =>
    {
        meta.DisplayName("ERP Buffer");
    });
});
```

Each `AddDataFlows()` call contributes its metadata independently — multiple modules can each configure metadata for their own blocks and all entries are merged together:

```csharp
// Module A
services.AddDataFlows("module-a", df =>
{
    df.AddActorBlock<In, Out, ActorA>("producer", meta => meta.DisplayName("A Producer"));
});

// Module B — does not interfere with Module A's metadata
services.AddDataFlows("module-b", df =>
{
    df.AddActorBlock<In, Out, ActorB>("worker", meta => meta.DisplayName("B Worker"));
});
// Both "A Producer" and "B Worker" are present in IDataFlowBlockMetadataStore
```

> **Note:** Block names in the callback are short names (e.g. `"erp-poster-1"`); the namespace prefix (`"journal-v2:"`) is applied automatically. The fully-qualified key (e.g. `"journal-v2:erp-poster-1"`) is what appears in the diagram and events.

### Flow-level display name

To set a human-readable label for the entire flow (shown as the panel header instead of the raw namespace key), call `DisplayName()` on the builder:

```csharp
services.AddDataFlows("journal-v2", df =>
{
    df.DisplayName("Journal Processing Flow");
    df.AddActorBlock<Invoice, PostedInvoice, ErpPosterActor>("erp-poster-1", meta =>
        meta.DisplayName("ERP Poster"));
    // ...
});
```

The method is chainable:

```csharp
services.AddDataFlows("journal-v2", df => df
    .DisplayName("Journal Processing Flow")
    .AddActorBlock<Invoice, PostedInvoice, ErpPosterActor>("erp-poster-1",
        meta => meta.DisplayName("ERP Poster")));
```

### What the diagram does with this information

- **DisplayName** replaces the block title. If not set, the registered name is used,
  truncated to 18 characters with an ellipsis if needed. The full name is always
  visible on hover via an SVG tooltip.
- **TypeLabel** replaces the type label beneath the title. If not set,
  `DataFlowBlockTypeNameFormatter` provides the default for native types;
  unrecognised types fall back to `Type.Name`.

### Default type labels for native block types

| Block class | Default label |
|---|---|
| `EpochActorBlock<TIn, TOut, TActor>` | Actor |
| `EpochBufferBlock<T>` | Buffer |
| `EpochBatchBlock<T>` | Batch |
| `EpochSourceBlock<T, TActor>` | Source |

Any other type gets `Type.Name` (the raw CLR name) unless a `TypeLabel` override is configured.

---

## Step 10 — Optional: custom block detail components

The detail pane that appears when a user clicks a block on the diagram can be
replaced per block type with a custom Blazor component. This lets you show
domain-specific information (e.g. current batch keys, DLQ depth, retry count)
alongside the standard metrics.

Register components at startup in the Blazor client `Program.cs`:

```csharp
builder.Services.Configure<BlockDetailViewOptions>(opts =>
{
    opts.Register("InvoiceValidatorBlock", typeof(InvoiceValidatorDetailView));
    opts.Register("HttpSourceBlock",       typeof(HttpSourceDetailView));
});
```

Each registered component must declare a `[Parameter] public BlockState BlockState { get; set; }`
property. The library injects the live `BlockState` on each render cycle.

If no custom component is registered for a block type the default detail view
(metrics + status + timing) is shown.

---

## Step 11 — Optional theming

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
- [ ] Hub mapped in server `Program.cs` — either via `app.MapDataFlowEndpoints()` / `app.MapDataFlowEndpoints<TContext>()` (convenience), or via `app.MapDataFlowHttpEndpoints()` + `app.MapHub<FlowEventsHub<TContext>>("/path")` (app-controlled)
- [ ] If using a custom hub path: `hubPath` in `AddDataFlowVisualizationClient` matches the path used in `MapHub`
- [ ] If BYO-context: `modelBuilder.AddDataFlowVisualizationEntities()` called in `OnModelCreating`
- [ ] EF schema creation/migration applied
- [ ] `AddDataFlowVisualizationClient(baseUrl: …)` in client `Program.cs`
- [ ] Both `<link>` tags in host HTML
- [ ] `@using` directives in `_Imports.razor`
- [ ] `<FlowVisualization InvocationId="…" />` on a routable page
- [ ] `IFlowEventSink` / `BoundFlowEventEmitter` wired into the pipeline execution code
- [ ] Block display names / type labels configured where block registered names are long or non-native block types need friendly labels (Step 9b — optional)
- [ ] Solution builds (`dotnet build`)
- [ ] Navigate to the monitor page in a browser and verify the visualization loads

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| CS1061 `AddDataFlowVisualizationClient` not found | Missing `using DataFlow.Blazor.Extensions;` in the Blazor client `Program.cs` |
| CS1061 `AddDataFlowVisualizationEntities` not found | Missing `using DataFlow.Blazor.Server.Persistence;` in the DbContext file |
| CS0311 `IEndpointRouteBuilder` cannot be used as `TBuilder` for `RequireAuthorization` | `MapDataFlowEndpoints()` returns `IEndpointRouteBuilder`, not `IEndpointConventionBuilder` — you cannot chain `.RequireAuthorization()` on it. Use `MapDataFlowHttpEndpoints` + `app.MapHub<...>(path).RequireAuthorization()` instead |
| Component renders but has no styling | Missing `<link>` tags in host HTML — ensure **both** links are present (`.bundle.scp.css` **and** `dataflow-blazor.css`) |
| Detail pane appears below the diagram instead of beside it | The `.bundle.scp.css` link is missing or uses the wrong filename (e.g. `.styles.css`). Without this file the `diagram-area` flex row layout is not applied and the pane stacks below the SVG. Verify the link is present and reload. |
| Colours are default but theming overrides not working | App stylesheet loaded **before** library stylesheet — swap order |
| SignalR connection refused | Hub path mismatch: the path passed to `MapDataFlowEndpoints` (or `MapHub` if registering manually) must exactly match `hubPath` in `AddDataFlowVisualizationClient` |
| App uses Azure SignalR Service | No special steps needed. Use `MapDataFlowHttpEndpoints` and map the hub yourself so you can apply your existing Azure SignalR options. `FlowEventsHub` is transport-agnostic — Azure SignalR intercepts the transport layer transparently. The library's internal `AddSignalR()` call is idempotent and will not override your Azure SignalR setup. |
| BYO-context: EF can't find the tables | `modelBuilder.AddDataFlowVisualizationEntities()` not called in `OnModelCreating`, or migration not applied |
| "Loading flow visualization…" never resolves | `IEventSource` not registered, or server endpoints not mapped |
| EF exception on first run | EF provider package not installed, or `EnsureCreated()` not called |
| No events appear despite the pipeline running | `IFlowEventSink` not resolved from `IExecutionContext.ServiceProvider` — check DI wiring |
| Flow stuck showing RUNNING after completion | `FlowCompletedEvent` not emitted — ensure it is always sent, even on exception paths |
| Buffer health pills absent | `ChannelStatsEvent` not being emitted — check edge event wiring |
| Block stat counts not showing | `BlockMetricsEvent` not emitted, or `ItemsConsumed`/`ItemsProduced` both zero |
| Competing-consumer edges look the same as broadcast | Running an older version of the library that pre-dates `EdgeType` in `FlowGraphDefinedEvent`. Upgrade both `Uniun.DataFlow` and `Uniun.DataFlow.Blazor` packages to the same version. |

---

## Reference

- Component README: `poc/DataFlow.Blazor/README.md`
- Theming guide:   `docs/guides/blazor-theming.md`
- Server extension:`DataFlow.Blazor.Server/Extensions/DataFlowVisualizationServerExtensions.cs`
- Client extension:`DataFlow.Blazor/Extensions/DataFlowVisualizationClientExtensions.cs`
- Event model:     `DataFlow.Blazor.Shared/Events/`
