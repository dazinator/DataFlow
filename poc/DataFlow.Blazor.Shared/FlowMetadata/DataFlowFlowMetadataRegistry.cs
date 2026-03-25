namespace DataFlow.Blazor.FlowMetadata;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Mutable singleton registry that accumulates flow metadata from all registration calls.
/// Ensures that multiple <c>AddDataFlows()</c> calls across different modules each contribute
/// their flow metadata entries without overwriting each other.
/// </summary>
internal sealed class DataFlowFlowMetadataRegistry : IDataFlowFlowMetadataStore
{
    private readonly Dictionary<string, DataFlowFlowMetadata> _metadata = new();

    /// <summary>
    /// Registers or overwrites the metadata entry for a single flow.
    /// </summary>
    public void Set(string flowName, DataFlowFlowMetadata metadata) =>
        _metadata[flowName] = metadata;

    public DataFlowFlowMetadata? GetMetadata(string flowName) =>
        _metadata.TryGetValue(flowName, out var m) ? m : null;

    public string? GetDisplayName(string flowName) =>
        _metadata.TryGetValue(flowName, out var m) ? m.DisplayName : null;

    /// <summary>
    /// Gets the existing <see cref="DataFlowFlowMetadataRegistry"/> registered in
    /// <paramref name="services"/>, or creates and registers a new one if absent.
    /// </summary>
    internal static DataFlowFlowMetadataRegistry GetOrCreate(IServiceCollection services)
    {
        var existingDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IDataFlowFlowMetadataStore) &&
            d.ImplementationInstance is DataFlowFlowMetadataRegistry);

        if (existingDescriptor?.ImplementationInstance is DataFlowFlowMetadataRegistry existing)
            return existing;

        var registry = new DataFlowFlowMetadataRegistry();
        services.AddSingleton<IDataFlowFlowMetadataStore>(registry);
        return registry;
    }
}
