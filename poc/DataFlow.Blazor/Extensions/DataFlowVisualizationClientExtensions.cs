namespace DataFlow.Blazor.Extensions;

using DataFlow.Blazor.Events;
using DataFlow.Blazor.Models;
using DataFlow.Blazor.Services;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Blazor WASM client registration for DataFlow visualization.
///
/// Usage in Blazor WASM Program.cs:
/// <code>
/// // Use the real HTTP+SignalR backend (requires Uniun.DataFlow.Blazor.Server on the host)
/// builder.Services.AddDataFlowVisualizationClient(
///     baseUrl: builder.HostEnvironment.BaseAddress,
///     hubPath: "/hubs/flow-events");
///
/// // With JWT authentication (e.g. when the hub requires authorization):
/// builder.Services.AddDataFlowVisualizationClient(
///     baseUrl: builder.HostEnvironment.BaseAddress,
///     accessTokenProvider: async () => await tokenService.GetTokenAsync());
///
/// // Or during development, keep the mock event source:
/// builder.Services.AddScoped&lt;IEventSource, MockEventSource&gt;();
/// </code>
/// </summary>
public static class DataFlowVisualizationClientExtensions
{
    /// <summary>
    /// Registers the HTTP+SignalR event source that connects to the ASP.NET Core
    /// server integration (Uniun.DataFlow.Blazor.Server).
    /// </summary>
    /// <param name="services">The Blazor WASM service collection.</param>
    /// <param name="baseUrl">Base URL of the ASP.NET Core host (e.g. builder.HostEnvironment.BaseAddress).</param>
    /// <param name="hubPath">SignalR hub path registered on the server (default: /hubs/flow-events).</param>
    /// <param name="accessTokenProvider">
    /// Optional factory that returns a bearer token for authenticated SignalR hubs.
    /// Use this when the hub is protected with <c>.RequireAuthorization()</c> and the server
    /// uses the standard ASP.NET Core query-string token middleware to read the JWT from
    /// WebSocket connections (i.e. the token is passed as the <c>access_token</c> query parameter).
    /// Example: <c>async () => await tokenService.GetTokenAsync()</c>
    /// </param>
    public static IServiceCollection AddDataFlowVisualizationClient(
        this IServiceCollection services,
        string baseUrl,
        string hubPath = "/hubs/flow-events",
        Func<Task<string?>>? accessTokenProvider = null)
    {
        var hubUrl = baseUrl.TrimEnd('/') + hubPath;

        services.AddScoped<IEventSource>(sp =>
        {
            var http = sp.GetRequiredService<HttpClient>();
            return new HttpSignalREventSource(http, hubUrl, accessTokenProvider);
        });

        services.AddScoped<IFlowListSource>(sp =>
        {
            var http = sp.GetRequiredService<HttpClient>();
            return new HttpFlowListSource(http);
        });

        services.AddOptions<BlockDetailViewOptions>();

        return services;
    }
}
