# Flow Visualization — Integration Guide

This guide covers how to add real-time flow visualization and run history to your own
Blazor application. It assumes you already have a working Blazor WASM + ASP.NET Core
hosted application and one or more `DataFlowGraph` definitions.

---

## Packages

| Package | Where to install | Purpose |
|---|---|---|
| `Uniun.DataFlow.Blazor.Server` | ASP.NET Core host | EF Core persistence, SignalR hub, HTTP endpoints |
| `Uniun.DataFlow.Blazor` | Blazor WASM project | UI components, event source services |
| `Uniun.DataFlow.Blazor.Shared` | Both (transitive) | Shared DTOs and event types — pulled in automatically |

```xml
<!-- ASP.NET Core host (.csproj) -->
<PackageReference Include="Uniun.DataFlow.Blazor.Server" Version="*" />

<!-- Blazor WASM project (.csproj) -->
<PackageReference Include="Uniun.DataFlow.Blazor" Version="*" />
```

---

## Server Setup

### 1. Register services

In your ASP.NET Core `Program.cs`, call `AddDataFlowVisualizationServer` with your
EF Core provider. The library is provider-agnostic — pass whichever EF Core backend
your application uses.

**Azure SQL / SQL Server:**

```csharp
builder.Services.AddDataFlowVisualizationServer(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DataFlowViz")));
```

**SQLite (development / local):**

```csharp
builder.Services.AddDataFlowVisualizationServer(options =>
    options.UseSqlite("Data Source=dataflow-viz.db"));
```

**Optional — tune the snapshot interval:**

A snapshot is always written when a flow starts and when it completes. You can also
configure how often a snapshot is taken during a long-running flow (default: every
100 events). Set to `0` to disable mid-run snapshots entirely.

```csharp
builder.Services.AddDataFlowVisualizationServer(
    options => options.UseSqlServer(...),
    periodicSnapshotInterval: 50);   // snapshot every 50 events
```

### 2. Map endpoints and the SignalR hub

```csharp
app.MapDataFlowEndpoints();                         // GET /flows, GET /flows/{id}/state
app.MapHub<FlowEventsHub>("/hubs/flow-events");     // SignalR live stream
```

### 3. Create the schema

For development, `EnsureCreated` is the quickest option:

```csharp
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider
        .GetRequiredService<FlowVisualizationDbContext>()
        .Database.EnsureCreated();
}
```

For production use EF Core migrations instead:

```bash
dotnet ef migrations add InitialCreate --project YourHost.csproj
dotnet ef database update
```

### Full server Program.cs example

```csharp
using DataFlow.Blazor.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataFlowVisualizationServer(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DataFlowViz")));

// ... your other services

var app = builder.Build();

app.MapDataFlowEndpoints();
app.MapHub<FlowEventsHub>("/hubs/flow-events");

// ... your other middleware / endpoints
```

---

## Connecting Your Flows to the Visualization

No code changes are needed to your `DataFlowGraph` definitions. The graph automatically
resolves `IFlowEventSink` from the `IExecutionContext.ServiceProvider` at runtime and
emits events for every flow and block lifecycle transition. As long as
`AddDataFlowVisualizationServer` has been called, the sink is in the container and
events will be persisted and pushed to clients.

The only requirement is that the `ExecutionContext` you pass to `graph.ExecuteAsync`
is built from a DI scope that descends from the host container:

```csharp
// ✅ Correct — scope comes from the host container, IFlowEventSink is resolvable
using var scope = _services.CreateScope();
var ctx = new ExecutionContext(scope.ServiceProvider, cancellationToken, invocationId);
await graph.ExecuteAsync(ctx);

// ❌ Won't emit events — manually constructed ServiceProvider has no IFlowEventSink
var ctx = new ExecutionContext(new ServiceCollection().BuildServiceProvider(), ...);
```

### Choosing a flow name

The name shown in the visualization and run list comes from the `DataFlowGraph`
constructor:

```csharp
var graph = new DataFlowGraph("invoice-processing", _logger);
```

Use a stable, human-readable name. If you run the same graph definition multiple times,
each invocation gets its own `InvocationId` and row in the run list while sharing the
same display name.

### Emitting custom events from blocks

For progress updates or channel stats, inject `IFlowEventEmitter` from the execution
context rather than calling `IFlowEventSink` directly. The emitter is pre-bound to the
current flow's `InvocationId`:

```csharp
public override async IAsyncEnumerable<TOut> ExecuteAsync(
    IAsyncEnumerable<TIn> input,
    IExecutionContext context)
{
    long processed = 0;
    await foreach (var item in input)
    {
        // ... process item ...
        processed++;
        await context.Events?.EmitAsync(
            new BlockProgressEvent(Name, processed, DateTime.UtcNow));
        yield return result;
    }
}
```

---

## Client Setup

### 1. Register the visualization client

In your Blazor WASM `Program.cs`:

```csharp
using DataFlow.Blazor.Extensions;

builder.Services.AddDataFlowVisualizationClient(
    baseUrl: builder.HostEnvironment.BaseAddress);
```

If you registered the SignalR hub at a non-default path, pass it explicitly:

```csharp
builder.Services.AddDataFlowVisualizationClient(
    baseUrl: builder.HostEnvironment.BaseAddress,
    hubPath: "/hubs/my-flow-events");   // must match app.MapHub<FlowEventsHub>(...)
```

#### Authenticated hubs

If the hub is protected with `.RequireAuthorization()` (e.g. the server uses JWT bearer
auth and the standard query-string token middleware), pass an `accessTokenProvider`:

```csharp
builder.Services.AddDataFlowVisualizationClient(
    baseUrl: builder.HostEnvironment.BaseAddress,
    accessTokenProvider: async () =>
    {
        // Return the raw JWT (without "Bearer " prefix).
        // Replace this with however your app provides tokens — e.g.:
        //   Microsoft.AspNetCore.Components.WebAssembly.Authentication
        //   Blazored.LocalStorage
        //   a custom ITokenService
        var tokenResult = await tokenProvider.RequestAccessToken();
        return tokenResult.TryGetToken(out var token) ? token.Value : null;
    });
```

The token is sent by the SignalR client as the `access_token` query parameter on the
WebSocket/SSE connection — this is the standard mechanism ASP.NET Core uses to pass
tokens over transports that cannot set HTTP headers.

> **Note**: The HTTP catch-up request (`GET /flows/{id}/state`) uses the normal
> `HttpClient` that is already registered in your DI container. Ensure that client
> has the appropriate `Authorization` header set (e.g. via a `DelegatingHandler` or
> `IHttpClientFactory` named client) if that endpoint is also protected.

### 2. Add the using to `_Imports.razor`

```razor
@using DataFlow.Blazor.Components
```

---

## Components

### `<FlowVisualization>`

Renders the live topology diagram and status panel for a single flow run. Subscribes
to SignalR automatically and updates in real time.

```razor
<FlowVisualization InvocationId="@myInvocationId" />
```

| Parameter | Type | Required | Description |
|---|---|---|---|
| `InvocationId` | `Guid` | Yes | The `InvocationId` returned when the flow was started |
| `EventSourceOverride` | `IEventSource?` | No | Inject a mock event source for testing (defaults to the registered `IEventSource`) |

**Typical usage — show visualization after starting a flow:**

```razor
@inject HttpClient Http

@if (_invocationId.HasValue)
{
    <FlowVisualization InvocationId="@_invocationId.Value" />
}

<button @onclick="StartFlow">Run</button>

@code {
    private Guid? _invocationId;

    private async Task StartFlow()
    {
        var result = await Http.PostAsJsonAsync("/your-flow/run", (object?)null);
        var body = await result.Content.ReadFromJsonAsync<RunResult>();
        _invocationId = body?.InvocationId;
    }

    private record RunResult(Guid InvocationId);
}
```

The component handles its own loading state and cleans up its SignalR connection when
navigated away from.

---

### `<FlowRunsList>`

Renders a polling table of all flow runs (running and completed) with status badges,
duration, and a View button that navigates to a detail page. Refreshes every 3 seconds
automatically.

When a run has `AttemptNumber > 1` (i.e. it is a retry of a queue-driven flow), an amber
**attempt N** badge is shown next to the flow name. Hovering the badge shows the full
`CorrelationId` for cross-referencing.

```razor
<FlowRunsList />
```

No parameters — the component uses the registered `IFlowListSource` internally.

**Typical usage — a flows dashboard page:**

```razor
@page "/flows"
@using DataFlow.Blazor.Components

<h1>Flow Runs</h1>
<button class="btn btn-primary" @onclick="StartFlow">Start New Run</button>

<FlowRunsList />

@code {
    private async Task StartFlow() { ... }
}
```

The View button in each row navigates to `/flows/{invocationId}`. Create a page at
that route that renders `<FlowVisualization>` (see below).

---

## Putting It Together — Minimal End-to-End Example

### Server: expose a "start flow" endpoint

```csharp
app.MapPost("/invoice-processing/run", (InvoiceFlowRunner runner) =>
{
    var invocationId = runner.StartRun();
    return Results.Ok(new { invocationId });
});
```

```csharp
public class InvoiceFlowRunner
{
    private readonly IServiceProvider _services;
    private readonly ILogger<InvoiceFlowRunner> _logger;

    public InvoiceFlowRunner(IServiceProvider services, ILogger<InvoiceFlowRunner> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Guid StartRun()
    {
        var invocationId = Guid.NewGuid();
        _ = Task.Run(() => ExecuteAsync(invocationId));
        return invocationId;
    }

    private async Task ExecuteAsync(Guid invocationId)
    {
        using var scope = _services.CreateScope();

        var graph = new DataFlowGraph("invoice-processing", /* logger */);
        // ... add blocks and edges ...

        var ctx = new ExecutionContext(scope.ServiceProvider, CancellationToken.None, invocationId);
        await graph.ExecuteAsync(ctx);
    }
}
```

### Client: flows dashboard (home page)

```razor
@page "/"
@using DataFlow.Blazor.Components
@inject HttpClient Http

<h1>Invoice Processing</h1>

<button class="btn btn-primary" @onclick="StartRun">Start New Run</button>
@if (_error is not null) { <span style="color:red">@_error</span> }

<FlowRunsList />

@code {
    private string? _error;

    private async Task StartRun()
    {
        _error = null;
        var result = await Http.PostAsJsonAsync("/invoice-processing/run", (object?)null);
        if (!result.IsSuccessStatusCode)
            _error = $"Failed: {result.StatusCode}";
    }
}
```

### Client: flow detail page

```razor
@page "/flows/{InvocationId:guid}"
@using DataFlow.Blazor.Components

<a href="/">← Back</a>
<FlowVisualization InvocationId="@InvocationId" />

@code {
    [Parameter] public Guid InvocationId { get; set; }
}
```

---

## HTTP Endpoints Reference

These are mapped automatically by `MapDataFlowEndpoints()`.

| Method | Path | Description |
|---|---|---|
| `GET` | `/flows` | Returns `FlowSummaryDto[]` — all known flow runs, newest first |
| `GET` | `/flows/{flowRunId}/state` | Returns snapshot + delta events for a single flow (used internally by `FlowVisualization`) |

### `GET /flows` response shape

```json
[
  {
    "flowRunId": "3fa85f64-...",
    "flowName": "invoice-processing",
    "status": "Completed",
    "startedAt": "2026-03-20T14:01:03Z",
    "completedAt": "2026-03-20T14:01:07Z",
    "errorMessage": null,
    "blockCount": 4,
    "triggerParamsJson": null,
    "correlationId": null,
    "attemptNumber": 1
  }
]
```

`status` is one of: `NotStarted` | `Running` | `Completed` | `Failed`

`correlationId` is `null` for standalone runs. For queue-driven flows it holds the stable
message identity shared across all retry attempts. `attemptNumber` is 1-based and increments
with each redelivery — see [Queue-based invocation with retry correlation](#queue-based-invocation-with-retry-correlation) below.

---

---

## Queue-based Invocation with Retry Correlation

When flows are driven by a message broker (Azure Service Bus, RabbitMQ, etc.) the broker
may redeliver the same message if the consumer crashes before acknowledging. Each delivery
should produce an independent event stream, but the visualization can surface that they
are retries of the same logical work item.

Pass two optional fields when constructing the `ExecutionContext`:

| Field | Source | Description |
|---|---|---|
| `correlationId` | `message.MessageId` (or equivalent) | Stable identity of the work item — shared across all retries |
| `attemptNumber` | `message.DeliveryCount` (1-based) | Which delivery this is |

```csharp
// Azure Service Bus example
public async Task ProcessMessageAsync(ServiceBusReceivedMessage message, ...)
{
    var invocationId = Guid.NewGuid();           // always fresh per attempt
    var correlationId = Guid.Parse(message.MessageId);
    var attemptNumber = message.DeliveryCount;   // 1 on first delivery

    using var scope = _services.CreateScope();
    var ctx = new ExecutionContext(
        scope.ServiceProvider,
        cancellationToken,
        invocationId,
        correlationId: correlationId,
        attemptNumber: attemptNumber);

    var graph = new DataFlowGraph("invoice-processing", _logger);
    // ... add blocks and edges ...

    await graph.ExecuteAsync(ctx);
}
```

The visualization will:
- Show each attempt as a separate row in `<FlowRunsList>` (each has its own independent event stream)
- Display an amber **attempt N** badge next to the flow name for any run where `AttemptNumber > 1`

Standalone / ad-hoc runs that don't pass these fields default to `correlationId = null` and
`attemptNumber = 1` — no badge is shown and behaviour is identical to before.

---

## Troubleshooting

**Events are not appearing in the visualization**

- Confirm `AddDataFlowVisualizationServer` is called before `app.Build()`.
- Confirm the `ExecutionContext` is built from a DI scope descended from the host
  container (not a manually constructed `IServiceProvider`).
- Confirm `app.MapHub<FlowEventsHub>(...)` path matches the `hubPath` passed to
  `AddDataFlowVisualizationClient`.

**Flow list is empty after running a flow**

- Confirm `app.MapDataFlowEndpoints()` is called.
- Confirm the schema exists — call `Database.EnsureCreated()` on startup, or run
  migrations.

**`DateTimeOffset` error on startup (SQLite only)**

This is a SQLite limitation. It does not affect SQL Server or Azure SQL. Ensure you are
on the latest version of this package where `GET /flows` orders by `AsOfEventId`
(a `long`) rather than `CreatedAt`.

**The diagram layout looks wrong for my topology**

The `FlowDiagram` component infers layout from block names and channel connections using
a topological sort. Blocks at the same depth are placed in the same column. If a block
is misclassified, check that its name does not inadvertently match the heuristics for
producer/router/buffer detection. Custom layout support is planned.
