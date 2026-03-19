namespace DataFlow.Blazor.Services;

using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DataFlow.Blazor.Api;
using DataFlow.Blazor.Events;
using DataFlow.Blazor.Projection;
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
/// </summary>
public class HttpSignalREventSource : IEventSource, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _hubUrl;

    // Cached per-invocation state (populated by GetSnapshotAsync)
    private FlowSnapshot? _cachedSnapshot;
    private FlowEventDto[]? _cachedDeltaEvents;
    private long _asOfId;

    public HttpSignalREventSource(HttpClient http, string hubUrl)
    {
        _http = http;
        _hubUrl = hubUrl;
    }

    public async Task<FlowSnapshot?> GetSnapshotAsync(Guid invocationId, CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<FlowStateResponse>(
            $"/flows/{invocationId}/state", JsonOptions, cancellationToken);

        if (response is null) return null;

        _cachedDeltaEvents = response.DeltaEvents;
        _asOfId = response.AsOfId;

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
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        var channel = System.Threading.Channels.Channel.CreateUnbounded<IDataFlowEvent>();

        hub.On<FlowEventDto>("EventAppended", dto =>
        {
            var evt = EventDeserializer.Deserialize(dto.EventType, dto.Payload);
            if (evt is not null)
                channel.Writer.TryWrite(evt);
        });

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

    public async ValueTask DisposeAsync()
    {
        // Resources are disposed inside GetEventsAsync finally block
        await ValueTask.CompletedTask;
    }
}
