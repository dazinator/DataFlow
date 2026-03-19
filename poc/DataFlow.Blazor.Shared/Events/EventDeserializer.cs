namespace DataFlow.Blazor.Events;

using System.Text.Json;

/// <summary>
/// Deserializes DataFlow events from their wire format (EventType discriminator + JSON payload).
/// Used by the Blazor WASM client when applying delta events from the catch-up endpoint
/// and live events pushed via SignalR.
/// </summary>
public static class EventDeserializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IDataFlowEvent? Deserialize(string eventType, string payload) => eventType switch
    {
        nameof(FlowStartedEvent)    => JsonSerializer.Deserialize<FlowStartedEvent>(payload, Options),
        nameof(FlowCompletedEvent)  => JsonSerializer.Deserialize<FlowCompletedEvent>(payload, Options),
        nameof(BlockStartedEvent)   => JsonSerializer.Deserialize<BlockStartedEvent>(payload, Options),
        nameof(BlockCompletedEvent) => JsonSerializer.Deserialize<BlockCompletedEvent>(payload, Options),
        nameof(BlockProgressEvent)  => JsonSerializer.Deserialize<BlockProgressEvent>(payload, Options),
        nameof(ChannelStatsEvent)   => JsonSerializer.Deserialize<ChannelStatsEvent>(payload, Options),
        _ => null
    };
}
