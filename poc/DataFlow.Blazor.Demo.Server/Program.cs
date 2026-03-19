using DataFlow.Blazor.Server;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------
// DataFlow Visualization — server-side services
// Registers: FlowVisualizationDbContext (SQLite), IFlowEventSink, SignalR
// -----------------------------------------------------------------------
builder.Services.AddDataFlowVisualizationServer(options =>
    options.UseSqlite("Data Source=dataflow-viz.db"));

// Serve the Blazor WASM client from this host
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Ensure the SQLite schema is created on first run
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DataFlow.Blazor.Server.Persistence.FlowVisualizationDbContext>();
    db.Database.EnsureCreated();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();

// -----------------------------------------------------------------------
// DataFlow Visualization — HTTP catch-up endpoint + SignalR hub
// GET /flows/{flowRunId}/state  →  snapshot + delta events
// WS  /hubs/flow-events         →  live event stream
// -----------------------------------------------------------------------
app.MapDataFlowEndpoints();
app.MapHub<FlowEventsHub>("/hubs/flow-events");

app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
