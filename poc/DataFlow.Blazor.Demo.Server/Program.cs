using DataFlow.Blazor.Server;
using DataFlow.Blazor.Demo.Server.Flows;
using DataFlow.Blazor.ItemTypes;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------
// DataFlow Visualization — server-side services
// Registers: FlowVisualizationDbContext (SQLite), IFlowEventSink, SignalR
// -----------------------------------------------------------------------
builder.Services.AddDataFlowVisualizationServer(options =>
    options.UseSqlite("Data Source=dataflow-viz.db"));

// Item type labels — shown in the "Items" panel in the flow visualization
builder.Services.AddDataFlowItemTypes(items =>
{
    items.ForType<int>().Label("item");
    items.ForType<List<int>>().Label("item batch");
});

// Demo flow runner — builds and executes the demo graphs
builder.Services.AddSingleton<DemoFlowRunner>();

// Serve the Blazor WASM client from this host
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Ensure the SQLite schema is created on first run, then apply any
// additive column migrations that EnsureCreated won't apply to existing DBs.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataFlow.Blazor.Server.Persistence.FlowVisualizationDbContext>();
    db.Database.EnsureCreated();

    // Additive column migration — safe to run on both new and existing databases.
    // EnsureCreated is a no-op on existing DBs, so we apply any new columns manually.
    var connection = db.Database.GetDbConnection();
    connection.Open();
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "PRAGMA table_info(FlowEventRecords)";
        using var reader = cmd.ExecuteReader();
        var hasCorrelationId = false;
        while (reader.Read())
            if (reader.GetString(1) == "CorrelationId") { hasCorrelationId = true; break; }

        if (!hasCorrelationId)
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE FlowEventRecords ADD COLUMN CorrelationId TEXT NULL";
            alter.ExecuteNonQuery();
        }
    }
    connection.Close();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();

// -----------------------------------------------------------------------
// DataFlow Visualization — HTTP catch-up endpoints + SignalR hub
// GET /flows                    →  flow run list
// GET /flows/{flowRunId}/state  →  snapshot + delta events
// WS  /hubs/flow-events         →  live event stream
// -----------------------------------------------------------------------
app.MapDataFlowEndpoints();

// -----------------------------------------------------------------------
// Demo run endpoints — trigger a real backend DataFlow graph
// POST /flows/run/linear    →  { invocationId }
// POST /flows/run/branching →  { invocationId }
// POST /flows/run/fanin     →  { invocationId }
// -----------------------------------------------------------------------
app.MapPost("/flows/run/{topology}", (string topology, DemoFlowRunner runner) =>
{
    var invocationId = topology.ToLowerInvariant() switch
    {
        "linear"        => runner.RunLinear(),
        "branching"     => runner.RunBranching(),
        "fanin"         => runner.RunFanIn(),
        "backpressure"  => runner.RunBackpressure(),
        "failure"       => runner.RunFailure(),
        "queue-message" => runner.RunQueueMessage(),
        _               => (Guid?)null
    };

    return invocationId is null
        ? Results.BadRequest(new { error = $"Unknown topology '{topology}'. Use: linear, branching, fanin, backpressure, failure." })
        : Results.Ok(new { invocationId });
});

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
