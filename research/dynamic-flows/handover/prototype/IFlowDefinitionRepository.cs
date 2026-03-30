namespace DataFlow.DynamicFlows.Prototype;

using System.Text.Json;

// ---------------------------------------------------------------------------
// IFlowDefinitionRepository
// Abstraction for persisting and retrieving flow definitions.
// Applications implement this; the library ships a default EF Core version.
// ---------------------------------------------------------------------------

/// <summary>
/// Abstraction for storing and retrieving <see cref="FlowDefinition"/> instances.
///
/// <para>
/// Applications register an implementation of this interface. The library ships
/// a default implementation — <c>EfCoreFlowDefinitionRepository</c> — backed by
/// the existing <c>FlowVisualizationDbContext</c>. Applications that need
/// alternative storage (blob, filesystem, external API) provide their own.
/// </para>
/// </summary>
public interface IFlowDefinitionRepository
{
    /// <summary>Persists or updates a flow definition. Creates a new version record.</summary>
    Task SaveAsync(FlowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Returns the latest version of the named flow, or null if not found.</summary>
    Task<FlowDefinition?> GetLatestAsync(string flowId, CancellationToken cancellationToken = default);

    /// <summary>Returns a specific version of a flow definition.</summary>
    Task<FlowDefinition?> GetVersionAsync(string flowId, int version, CancellationToken cancellationToken = default);

    /// <summary>Returns all known flow IDs.</summary>
    Task<IReadOnlyList<FlowDefinitionSummary>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>Summary row for the flow definition list view.</summary>
public sealed class FlowDefinitionSummary
{
    public string FlowId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int LatestVersion { get; init; }
    public DateTime LastModifiedAt { get; init; }
}

// ---------------------------------------------------------------------------
// InMemoryFlowDefinitionRepository
// Lightweight in-memory implementation suitable for demos and testing.
// ---------------------------------------------------------------------------

/// <summary>
/// In-memory implementation of <see cref="IFlowDefinitionRepository"/>.
/// Suitable for demos, integration tests, and applications that don't need persistence.
/// </summary>
public sealed class InMemoryFlowDefinitionRepository : IFlowDefinitionRepository
{
    private readonly Dictionary<string, List<(FlowDefinition Definition, DateTime SavedAt)>> _store = new();
    private readonly object _lock = new();

    public Task SaveAsync(FlowDefinition definition, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(definition.FlowId, out var versions))
            {
                versions = new List<(FlowDefinition, DateTime)>();
                _store[definition.FlowId] = versions;
            }

            versions.Add((definition, DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    public Task<FlowDefinition?> GetLatestAsync(string flowId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(flowId, out var versions) || versions.Count == 0)
                return Task.FromResult<FlowDefinition?>(null);

            return Task.FromResult<FlowDefinition?>(versions[^1].Definition);
        }
    }

    public Task<FlowDefinition?> GetVersionAsync(string flowId, int version, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(flowId, out var versions))
                return Task.FromResult<FlowDefinition?>(null);

            var match = versions.FirstOrDefault(v => v.Definition.Version == version);
            return Task.FromResult<FlowDefinition?>(match.Definition);
        }
    }

    public Task<IReadOnlyList<FlowDefinitionSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var summaries = _store
                .Where(kv => kv.Value.Count > 0)
                .Select(kv =>
                {
                    var latest = kv.Value[^1];
                    return new FlowDefinitionSummary
                    {
                        FlowId = kv.Key,
                        Name = latest.Definition.Name,
                        LatestVersion = latest.Definition.Version,
                        LastModifiedAt = latest.SavedAt
                    };
                })
                .ToList();

            return Task.FromResult<IReadOnlyList<FlowDefinitionSummary>>(summaries);
        }
    }
}
