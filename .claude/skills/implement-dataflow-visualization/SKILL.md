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

## Step 1 — Understand the target solution structure

Before making any changes, read the solution to answer these questions:

- Is this a **hosted Blazor WASM** solution (separate `.Client` and `.Server`
  projects sharing one ASP.NET Core host), a **Blazor Server** app, or a
  **standalone WASM** app?
- Which project hosts the ASP.NET Core pipeline (`Program.cs` with
  `WebApplication.CreateBuilder`)?
- Which project is the Blazor client (`Program.cs` with
  `WebAssemblyHostBuilder.CreateDefault`)?
- Is there an existing EF Core `DbContext`? Which provider (SQLite, SQL Server,
  PostgreSQL)?
- Does the solution already reference any Uniun.DataFlow packages?

Use `Glob` and `Read` to answer these before proceeding.

---

## Step 2 — Install NuGet packages

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
- `<FlowVisualization>` — root component, drop onto any page
- `IEventSource` / `HttpSignalREventSource` — client-side event streaming
- `AddDataFlowVisualizationClient()` extension method

---

## Step 3 — Server-side wiring (`Program.cs` of the ASP.NET Core host)

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

## Step 4 — Client-side DI registration (`Program.cs` of the Blazor WASM project)

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

## Step 5 — CSS setup in the host HTML

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

## Step 6 — Add `@using` to `_Imports.razor`

In the Blazor client project's `_Imports.razor`:

```razor
@using DataFlow.Blazor.Components
@using DataFlow.Blazor.Events
```

---

## Step 7 — Drop the component onto a page

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

Or render it for a known invocation ID obtained from navigation state,
a service, or a route parameter.

---

## Step 8 — Wire up `IFlowEventSink` in the DataFlow execution code

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

        // Emit flow started
        await emitter.EmitAsync(new FlowStartedEvent(
            invocationId, "My Pipeline", DateTime.UtcNow));

        // Run your pipeline blocks — emit progress events per block:
        await emitter.EmitAsync(new BlockStartedEvent("extract", "HttpSourceBlock", DateTime.UtcNow));
        // ... do work ...
        await emitter.EmitAsync(new BlockProgressEvent("extract", itemsProcessed, DateTime.UtcNow));
        await emitter.EmitAsync(new BlockCompletedEvent("extract", success: true, DateTime.UtcNow));

        // Emit flow completed
        await emitter.EmitAsync(new FlowCompletedEvent(invocationId, success: true, DateTime.UtcNow));

        // Navigate the user (or provide a link) to:
        //   /flow-monitor/{invocationId}
    }
}
```

> **Security note**: `TriggerParamsJson` on `FlowStartedEvent` is stored
> verbatim and sent to all connected browser clients. Never include secrets,
> API keys, or PII in trigger parameters.

---

## Step 9 — Optional theming

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

---

## Reference

- Component README: `poc/DataFlow.Blazor/README.md`
- Theming guide:   `docs/guides/blazor-theming.md`
- Server extension:`DataFlow.Blazor.Server/Extensions/DataFlowVisualizationServerExtensions.cs`
- Client extension:`DataFlow.Blazor/Extensions/DataFlowVisualizationClientExtensions.cs`
- Event model:     `DataFlow.Blazor.Shared/Events/`
