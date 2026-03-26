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
///     accessTokenProvider: sp =>
///     {
///         var tokenService = sp.GetRequiredService&lt;IMyTokenService&gt;();
///         return async () => await tokenService.GetTokenAsync();
///     });
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
    /// Optional factory that receives the scoped <see cref="IServiceProvider"/> and returns a
    /// <c>Func&lt;Task&lt;string?&gt;&gt;</c> used to supply bearer tokens for authenticated SignalR hubs.
    /// The inner delegate is called by the SignalR client before each connection attempt; the returned
    /// token is forwarded as the <c>access_token</c> query parameter — the standard ASP.NET Core
    /// mechanism for authenticating WebSocket/SSE connections.
    /// Receiving the <see cref="IServiceProvider"/> allows token services registered in DI (e.g.
    /// <c>ITokenService</c>, MSAL) to be resolved without needing a captured closure.
    /// Example:
    /// <code>
    /// accessTokenProvider: sp =>
    /// {
    ///     var svc = sp.GetRequiredService&lt;IMyTokenService&gt;();
    ///     return async () => await svc.GetTokenAsync();
    /// }
    /// </code>
    /// </param>
    public static IServiceCollection AddDataFlowVisualizationClient(
        this IServiceCollection services,
        string baseUrl,
        string hubPath = "/hubs/flow-events",
        Func<IServiceProvider, Func<Task<string?>>>? accessTokenProvider = null)
    {
        var hubUrl = baseUrl.TrimEnd('/') + hubPath;

        services.AddScoped<IEventSource>(sp =>
        {
            var http = sp.GetRequiredService<HttpClient>();
            var tokenDelegate = accessTokenProvider?.Invoke(sp);
            return new HttpSignalREventSource(http, hubUrl, tokenDelegate);
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
