# DataFlow.Blazor - Real-time DataFlow Visualization Components

A Blazor Razor Class Library (RCL) providing real-time visualization components for monitoring DataFlow pipeline executions.

![DataFlow Visualization Demo](https://github.com/user-attachments/assets/e4316598-509f-42ec-97de-359475a4be73)

## Features

✅ **Real-time Visualization** - Live updates as your DataFlow executes  
✅ **SVG-based DAG Diagram** - Clear visualization of block connections and data flow  
✅ **Status Panel** - High-level metrics and flow status at a glance  
✅ **Block States** - Visual indicators for idle, running, completed, and failed states  
✅ **Channel Metrics** - Buffer utilization and throughput visualization  
✅ **Client-side Calculations** - Durations, items/sec, and buffer percentages computed in the browser  
✅ **Event-driven Architecture** - Pluggable event sources for future backend integration  

## Installation

```bash
dotnet add package Uniun.DataFlow.Blazor
```

## Quick Start

### 1. Register Services

In your `Program.cs`:

```csharp
using DataFlow.Blazor.Events;
using DataFlow.Blazor.Services;

builder.Services.AddSingleton<IEventSource, MockEventSource>();
// Or implement your own IEventSource for production use
```

### 2. Add the Component

In your Blazor page:

```razor
@page "/flow-monitor"
@using DataFlow.Blazor.Components

<FlowVisualization InvocationId="@_invocationId" />

@code {
    private Guid _invocationId = Guid.NewGuid();
}
```

### 3. Run Your App

The visualization will automatically connect to the event source and display real-time updates.

## CSS setup

Add both `<link>` tags to your host HTML file (`wwwroot/index.html` for hosted
WASM, `Components/App.razor` for Blazor Server) inside `<head>`:

```html
<!-- 1. Scoped component styles — always required -->
<link rel="stylesheet"
      href="_content/Uniun.DataFlow.Blazor/Uniun.DataFlow.Blazor.styles.css" />

<!-- 2. Default theme variables — required for colours, fonts, and radii -->
<link rel="stylesheet"
      href="_content/Uniun.DataFlow.Blazor/dataflow-blazor.css" />
```

No external CSS frameworks (Bootstrap, Tailwind, etc.) are needed.

### Theming

All visual tokens — colours, radii, shadows, and fonts — are exposed as CSS
custom properties (`--df-*`). Override any of them in your own stylesheet
(loaded **after** the library):

```css
/* app.css */
:root {
    --df-panel-header-from: #1a1a2e;
    --df-panel-header-to:   #16213e;
    --df-radius:            4px;
}
```

See [`docs/guides/blazor-theming.md`](../../docs/guides/blazor-theming.md) for
the full variable reference and a worked dark-theme example.

## Event Model

The library uses a simple event-driven architecture with the following event types:

### Flow Events
```csharp
FlowStartedEvent(Guid InvocationId, string FlowName, DateTime Timestamp)
FlowCompletedEvent(Guid InvocationId, bool Success, DateTime Timestamp, string? ErrorMessage)
```

### Block Events
```csharp
BlockStartedEvent(string BlockName, string BlockType, DateTime Timestamp)
BlockCompletedEvent(string BlockName, bool Success, DateTime Timestamp, string? ErrorMessage)
BlockProgressEvent(string BlockName, long ItemsProcessed, DateTime Timestamp)
```

### Channel Events
```csharp
ChannelStatsEvent(string BlockName, int BufferCapacity, int CurrentCount, DateTime Timestamp)
```

## Implementing a Custom Event Source

To integrate with your own backend, implement the `IEventSource` interface:

```csharp
public class MyEventSource : IEventSource
{
    public async IAsyncEnumerable<object> GetEventsAsync(
        Guid invocationId, 
        CancellationToken cancellationToken = default)
    {
        // Stream events from your backend (e.g., SignalR, gRPC, WebSockets)
        await foreach (var evt in _signalRClient.StreamAsync<object>($"flows/{invocationId}"))
        {
            yield return evt;
        }
    }

    public async Task<FlowSnapshot?> GetSnapshotAsync(
        Guid invocationId, 
        CancellationToken cancellationToken = default)
    {
        // Return aggregated snapshot for fast initial load
        return await _httpClient.GetFromJsonAsync<FlowSnapshot>(
            $"api/flows/{invocationId}/snapshot");
    }
}
```

## Component Architecture

```
FlowVisualization.razor (root component)
├── FlowStatusPanel.razor (summary metrics and status badge)
└── FlowDiagram.razor (SVG-based DAG visualization)
```

### State Management

The `EventProcessor` class maintains the current flow state by processing events:

```csharp
var processor = new EventProcessor(invocationId);

// Apply a snapshot for fast initial load
processor.ApplySnapshot(snapshot);

// Process real-time events
processor.ProcessEvent(new BlockStartedEvent("transform", "TransformBlock", DateTime.UtcNow));
processor.ProcessEvent(new BlockProgressEvent("transform", 100, DateTime.UtcNow));

// Access current state
var state = processor.State;
Console.WriteLine($"Flow: {state.FlowName}, Items: {state.TotalItemsProcessed}");
```

## Visual States

### Flow States
- **NotStarted** - Flow hasn't begun
- **Running** - Flow is executing (yellow badge, pulsing animation)
- **Completed** - Flow finished successfully (green badge)
- **Failed** - Flow encountered an error (red badge)

### Block States
- **Idle** - Block registered but not processing (gray)
- **Running** - Actively processing (yellow border, subtle pulse)
- **Completed** - Successfully finished (green border)
- **Failed** - Error occurred (red border)

### Channel Visualization
- Buffer utilization percentage displayed on edges
- Color intensity indicates throughput
- Bottleneck warnings when buffer >80% full

## Demo Application

A complete demo Blazor Server application is included in `DataFlow.Blazor.Demo`:

```bash
cd poc/DataFlow.Blazor.Demo
dotnet run
```

Navigate to `/flow-viz` to see the visualization in action with mock data.

## Architecture Decisions

### Why SVG Instead of Canvas?
- Accessibility (SVG elements are part of the DOM)
- CSS styling support
- Easy to inspect and debug
- No external diagram library dependency

### Why No Blazor.Diagrams Dependency?
- Minimal dependencies for better compatibility
- Full control over rendering and styling
- Smaller package size
- Easier to maintain

### Event-driven Design
- Decouples visualization from data source
- Supports multiple backends (SignalR, gRPC, WebSockets, polling)
- Easy to test with mock event generators
- Snapshot + real-time events pattern prevents lost updates

## Future Enhancements (Not in Phase 1)

The following features are planned for future releases:

- ❌ SignalR hub implementation for backend integration
- ❌ Event storage and historical playback
- ❌ Multi-flow monitoring dashboard
- ❌ Export visualization (SVG/PNG download)
- ❌ Alert configuration and notifications
- ❌ Authentication and authorization
- ❌ Zoom and pan controls for large graphs
- ❌ Custom block renderers

## Performance

The visualization is designed to handle:
- ✅ 100+ events/second without UI lag
- ✅ Real-time updates with 50ms throttling
- ✅ Efficient state management (incremental updates)
- ✅ Minimal re-renders (state change detection)

## Browser Support

- Chrome/Edge 90+
- Firefox 88+
- Safari 14+

## License

This library is part of the Uniun DataFlow project and follows the same dual-license model:
- **Non-Commercial**: AGPL-3.0
- **Commercial**: Requires separate license

## Contributing

Contributions are welcome! Please ensure:
- New features have corresponding tests
- Code follows existing patterns
- Documentation is updated

## Support

For issues and questions:
- GitHub Issues: https://github.com/uniun-technology/dataflow/issues
- Documentation: https://github.com/uniun-technology/dataflow

---

**Built with ❤️ by Uniun Technology Ltd**
