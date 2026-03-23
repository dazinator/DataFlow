using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DataFlow.Blazor.Extensions;
using DataFlow.Blazor.Demo.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Connect to the real ASP.NET Core backend via HTTP catch-up + SignalR
builder.Services.AddDataFlowVisualizationClient(builder.HostEnvironment.BaseAddress);

await builder.Build().RunAsync();
