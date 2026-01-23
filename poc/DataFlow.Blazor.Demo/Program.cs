using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using DataFlow.Blazor.Events;
using DataFlow.Blazor.Services;
using DataFlow.Blazor.Demo.Components;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register DataFlow.Blazor event sources
builder.Services.AddScoped<IEventSource, MockEventSource>();
builder.Services.AddScoped<BranchingMockEventSource>();
builder.Services.AddScoped<FanInMockEventSource>();

await builder.Build().RunAsync();
