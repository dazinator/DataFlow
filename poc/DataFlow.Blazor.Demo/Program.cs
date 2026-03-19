using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DataFlow.Blazor.Events;
using DataFlow.Blazor.Extensions;
using DataFlow.Blazor.Services;
using DataFlow.Blazor.Demo.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// -----------------------------------------------------------------------
// Event source configuration
//
// Option A — Real backend (requires DataFlow.Blazor.Demo.Server running):
//   Connects to the ASP.NET Core host via HTTP catch-up endpoint + SignalR.
//   Uncomment when running the hosted demo (poc/DataFlow.Blazor.Demo.Server).
// -----------------------------------------------------------------------
// builder.Services.AddDataFlowVisualizationClient(builder.HostEnvironment.BaseAddress);

// -----------------------------------------------------------------------
// Option B — Mock event sources (default, standalone WASM, no backend needed)
//   Useful for local UI development and testing without a database.
// -----------------------------------------------------------------------
builder.Services.AddScoped<IEventSource, MockEventSource>();

// Additional mock sources used by the branching/fan-in demo pages
builder.Services.AddScoped<BranchingMockEventSource>();
builder.Services.AddScoped<FanInMockEventSource>();

await builder.Build().RunAsync();
