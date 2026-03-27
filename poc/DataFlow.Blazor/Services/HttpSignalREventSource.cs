namespace DataFlow.Blazor.Services;

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DataFlow.Blazor.Api;
using DataFlow.Blazor.Events;
using DataFlow.Blazor.Projection;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;

/// <summary>
/// Real IEventSource implementation that connects to the ASP.NET Core server
/// provided by Uniun.DataFlow.Blazor.Server.
///
/// Load sequence:
/// 1. GetSnapshotAsync → calls GET /flows/{id}/state, caches snapshot + delta + asOfSequence
/// 2. GetEventsAsync   → yields cached delta events, then subscribes to SignalR from asOfSequence
///                       (race-condition safe: server replays any gap on Subscribe)
///
/// Register in Blazor WASM Program.cs:
/// <code>
/// builder.Services.AddDataFlowVisualizationClient(baseUrl: builder.HostEnvironment.BaseAddress);
/// </code>
///
/// For authenticated hubs, supply an access token provider:
/// <code>
/// builder.Services.AddDataFlowVisualizationClient(
///     baseUrl: builder.HostEnvironment.BaseAddress,
///     accessTokenProvider: sp => async () => await sp.GetRequiredService&lt;ITokenService&gt;().GetTokenAsync());
/// </code>
///
/// Or configure full connection options (transport type, headers, etc.):
/// <code>
/// builder.Services.AddDataFlowVisualizationClient(
///     baseUrl: builder.HostEnvironment.BaseAddress,
///     configureConnection: sp =>
///     {
///         var svc = sp.GetRequiredService&lt;ITokenService&gt;();
///         return options =>
///         {
///             options.AccessTokenProvider = async () => await svc.GetTokenAsync();
///             options.Transports = HttpTransportType.WebSockets;
///         };
///     });
/// </code>
/// </summary>
public class HttpSignalREventSource : IEventSource, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _hubUrl;
    private readonly Func<Task<string?>>? _accessTokenProvider;
    private readonly Action<HttpConnectionOptions>? _configureConnection;

    // Cached per-invocation state (populated by GetSnapshotAsync)
    private FlowSnapshot? _cachedSnapshot;
    private FlowEventDto[]? _cachedDeltaEvents;
    private FlowEventDto[]? _cachedAuditEvents;
    private long _asOfId;
    private string? _cachedFlowDisplayName;

    /// <param name="http">The HTTP client used for catch-up HTTP requests.</param>
    /// <param name="hubUrl">Full URL of the SignalR hub endpoint.</param>
    /// <param name="accessTokenProvider">
    /// Optional factory that returns a bearer token for authenticated hubs.
    /// The token is passed as the SignalR <c>access_token</c> query parameter,
    /// which is the standard mechanism used by ASP.NET Core's JWT middleware
    /// (and query-string token middleware) to authenticate WebSocket connections.
    /// When both <paramref name="accessTokenProvider"/> and <paramref name="configureConnection"/>
    /// are supplied, the token provider is applied last and will override any
    /// <see cref="HttpConnectionOptions.AccessTokenProvider"/> set by <paramref name="configureConnection"/>.
    /// </param>
    /// <param name="configureConnection">
    /// Optional callback to configure <see cref="HttpConnectionOptions"/> for the SignalR connection.
    /// Use this for full control over the connection — transport type, custom headers, access token, etc.
    /// Applied before <paramref name="accessTokenProvider"/>; if both are specified the token provider wins.
    /// </param>
    public HttpSignalREventSource(
        HttpClient http,
        string hubUrl,
        Func<Task<string?>>? accessTokenProvider = null,
        Action<HttpConnectionOptions>? configureConnection = null)
    {
        _http = http;
        _hubUrl = hubUrl;
        _accessTokenProvider = accessTokenProvider;
        _configureConnection = configureConnection;
    }

    public async Task<FlowSnapshot?> GetSnapshotAsync(Guid invocationId, CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<FlowStateResponse>(
            $"/flows/{invocationId}/state", JsonOptions, cancellationToken);

        if (response is null) return null;

        _cachedDeltaEvents = response.DeltaEvents;
        _cachedAuditEvents = response.AuditEvents;
        _asOfId = response.AsOfId;
        _cachedFlowDisplayName = response.FlowDisplayName;

        if (response.SnapshotJson is not null)
        {
            _cachedSnapshot = JsonSerializer.Deserialize<FlowSnapshot>(response.SnapshotJson, JsonOptions);
        }

        return _cachedSnapshot;
    }

    public async IAsyncEnumerable<IDataFlowEvent> GetEventsAsync(
        Guid invocationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. Yield cached delta events (events since the snapshot)
        if (_cachedDeltaEvents is not null)
        {
            foreach (var dto in _cachedDeltaEvents)
            {
                var evt = EventDeserializer.Deserialize(dto.EventType, dto.Payload);
                if (evt is not null) yield return evt;
            }
        }

        // 2. Subscribe to SignalR for live events
        var hub = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                _configureConnection?.Invoke(options);
                if (_accessTokenProvider is not null)
                    options.AccessTokenProvider = _accessTokenProvider;
            })
            .WithAutomaticReconnect()
            .Build();

        var channel = System.Threading.Channels.Channel.CreateUnbounded<IDataFlowEvent>();

        // Track the highest event Id received so that on reconnect we can ask the
        // server to replay only the gap — not the entire stream from _asOfId.
        var lastSeenId = _asOfId;
        var seenIds = new HashSet<long>();

        // The server joins the client to the group BEFORE running the gap-fill query,
        // so live events can arrive before gap-fill results. Buffer them and drain
        // in sorted order on "Subscribed" to guarantee the projection sees Id order.
        var buffer = new List<FlowEventDto>();
        var subscribed = false;

        void ProcessDto(FlowEventDto dto)
        {
            if (!seenIds.Add(dto.Id)) return;
            if (dto.Id > lastSeenId) lastSeenId = dto.Id;
            var evt = EventDeserializer.Deserialize(dto.EventType, dto.Payload);
            if (evt is not null) channel.Writer.TryWrite(evt);
        }

        hub.On<FlowEventDto>("EventAppended", dto =>
        {
            if (!subscribed) { buffer.Add(dto); return; }
            ProcessDto(dto);
        });

        hub.On("Subscribed", () =>
        {
            subscribed = true;
            foreach (var dto in buffer.OrderBy(d => d.Id))
                ProcessDto(dto);
            buffer.Clear();
        });

        // WithAutomaticReconnect re-establishes the transport but does not re-join
        // SignalR groups. Reset state and re-call Subscribe after each reconnect so
        // the client is added back to the flow's group and any gap is replayed.
        hub.Reconnected += async _ =>
        {
            subscribed = false;
            buffer.Clear();
            await hub.SendAsync("Subscribe", invocationId, lastSeenId, cancellationToken);
        };

        await hub.StartAsync(cancellationToken);

        // Pass asOfId so the server replays any gap between HTTP and WebSocket
        await hub.SendAsync("Subscribe", invocationId, _asOfId, cancellationToken);

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return evt;
            }
        }
        finally
        {
            channel.Writer.TryComplete();
            await hub.DisposeAsync();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IDataFlowEvent>> GetAuditLogAsync(
        Guid invocationId,
        CancellationToken cancellationToken = default)
    {
        if (_cachedAuditEvents is null or { Length: 0 })
            return Task.FromResult<IReadOnlyList<IDataFlowEvent>>([]);

        var events = _cachedAuditEvents
            .Select(dto => EventDeserializer.Deserialize(dto.EventType, dto.Payload))
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        return Task.FromResult<IReadOnlyList<IDataFlowEvent>>(events);
    }

    /// <inheritdoc />
    public string? FlowDisplayName => _cachedFlowDisplayName;

    public async ValueTask DisposeAsync()
    {
        // Resources are disposed inside GetEventsAsync finally block
        await ValueTask.CompletedTask;
    }
}
