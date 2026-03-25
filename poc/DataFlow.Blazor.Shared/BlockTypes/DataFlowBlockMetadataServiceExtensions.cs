namespace DataFlow.Blazor.BlockTypes;

using Microsoft.Extensions.DependencyInjection;

public static class DataFlowBlockMetadataServiceExtensions
{
    /// <summary>
    /// Registers block visualization metadata (display names, type labels) with the shared
    /// <see cref="IDataFlowBlockMetadataStore"/> singleton.
    ///
    /// <para>
    /// This method is safe to call from multiple modules — each call <em>merges</em> its entries
    /// into the shared store instead of replacing it. Later registrations for the same block name
    /// win (last-writer-wins per registration order).
    /// </para>
    ///
    /// <example>
    /// <code>
    /// // Module A
    /// services.AddDataFlowBlockMetadata(blocks =>
    /// {
    ///     blocks.ForBlock("module-a:producer").DisplayName("A Producer");
    /// });
    ///
    /// // Module B — does not overwrite Module A's entries
    /// services.AddDataFlowBlockMetadata(blocks =>
    /// {
    ///     blocks.ForBlock("module-b:processor").DisplayName("B Processor");
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public static IServiceCollection AddDataFlowBlockMetadata(
        this IServiceCollection services,
        Action<DataFlowBlockMetadataStoreBuilder> configure)
    {
        var storeBuilder = new DataFlowBlockMetadataStoreBuilder();
        configure(storeBuilder);
        DataFlowBlockMetadataRegistry.GetOrCreate(services).Merge(storeBuilder.GetEntries());
        return services;
    }

    /// <summary>
    /// Merges the supplied metadata entries into the shared <see cref="IDataFlowBlockMetadataStore"/>
    /// singleton, creating the store if it does not yet exist.
    /// Intended for use by infrastructure code (e.g. <c>DataFlowBuilder</c>) that collects
    /// metadata during service registration and needs to contribute it to the shared store.
    /// </summary>
    public static IServiceCollection MergeBlockMetadata(
        this IServiceCollection services,
        IReadOnlyDictionary<string, DataFlowBlockMetadata> entries)
    {
        DataFlowBlockMetadataRegistry.GetOrCreate(services).Merge(entries);
        return services;
    }
}
