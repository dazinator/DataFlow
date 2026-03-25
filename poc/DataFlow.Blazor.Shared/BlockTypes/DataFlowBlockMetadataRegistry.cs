namespace DataFlow.Blazor.BlockTypes;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Mutable singleton registry that accumulates block metadata from all registration calls.
/// Replaces the immutable <see cref="DataFlowBlockMetadataStore"/> as the registered
/// <see cref="IDataFlowBlockMetadataStore"/> singleton so that multiple modules can each
/// contribute metadata entries via <see cref="DataFlowBlockMetadataServiceExtensions.AddDataFlowBlockMetadata"/>
/// or <c>AddDataFlows()</c> without overwriting each other.
/// </summary>
internal sealed class DataFlowBlockMetadataRegistry : IDataFlowBlockMetadataStore
{
    private readonly Dictionary<string, DataFlowBlockMetadata> _metadata = new();

    /// <summary>
    /// Merges <paramref name="entries"/> into the registry.
    /// Later entries for the same block name win (last-writer-wins per call order).
    /// </summary>
    public void Merge(IReadOnlyDictionary<string, DataFlowBlockMetadata> entries)
    {
        foreach (var (key, value) in entries)
            _metadata[key] = value;
    }

    public DataFlowBlockMetadata? GetMetadata(string blockName) =>
        _metadata.TryGetValue(blockName, out var m) ? m : null;

    public string? GetDisplayName(string blockName) =>
        _metadata.TryGetValue(blockName, out var m) ? m.DisplayName : null;

    public string? GetTypeLabel(string blockName) =>
        _metadata.TryGetValue(blockName, out var m) ? m.TypeLabel : null;

    /// <summary>
    /// Gets the existing <see cref="DataFlowBlockMetadataRegistry"/> registered in
    /// <paramref name="services"/>, or creates and registers a new one if absent.
    /// </summary>
    internal static DataFlowBlockMetadataRegistry GetOrCreate(IServiceCollection services)
    {
        // Look for an existing registry instance already registered as the singleton.
        var existingDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IDataFlowBlockMetadataStore) &&
            d.ImplementationInstance is DataFlowBlockMetadataRegistry);

        if (existingDescriptor?.ImplementationInstance is DataFlowBlockMetadataRegistry existing)
            return existing;

        var registry = new DataFlowBlockMetadataRegistry();
        services.AddSingleton<IDataFlowBlockMetadataStore>(registry);
        return registry;
    }
}
