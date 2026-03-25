namespace DataFlow.Blazor.BlockTypes;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Carries the block visualization metadata contributed by a single <c>AddDataFlows()</c> call.
/// Multiple instances are registered as singletons and aggregated by
/// <see cref="DataFlowBlockMetadataStore"/> at first resolution using the standard
/// <see cref="IEnumerable{T}"/> DI pattern.
///
/// This type is part of the infrastructure used by <c>DataFlowBuilder</c> and is not
/// intended for direct use by application code. Configure metadata using the
/// per-block callback in <c>AddDataFlows</c>:
/// <code>
/// services.AddDataFlows("journal-v2", df =>
/// {
///     df.AddActorBlock&lt;In, Out, MyActor&gt;("worker",
///         meta => meta.DisplayName("My Worker"));
/// });
/// </code>
/// </summary>
public sealed class DataFlowBlockMetadataContribution
{
    /// <summary>Metadata entries keyed by fully-qualified block name.</summary>
    public IReadOnlyDictionary<string, DataFlowBlockMetadata> Entries { get; }

    /// <param name="entries">Entries collected by the builder for one <c>AddDataFlows</c> call.</param>
    public DataFlowBlockMetadataContribution(IReadOnlyDictionary<string, DataFlowBlockMetadata> entries)
        => Entries = entries;

    /// <summary>
    /// Registers <paramref name="entries"/> as a contribution and ensures the aggregating
    /// <see cref="IDataFlowBlockMetadataStore"/> singleton is registered exactly once.
    /// No-op when <paramref name="entries"/> is empty.
    /// Called by <c>DataFlowBuilder</c> — not intended for direct use.
    /// </summary>
    public static IServiceCollection Register(
        IServiceCollection services,
        IReadOnlyDictionary<string, DataFlowBlockMetadata> entries)
    {
        if (entries.Count == 0) return services;

        services.AddSingleton(new DataFlowBlockMetadataContribution(entries));

        // Register the aggregating store only once — it resolves all contributions at
        // construction time so every AddDataFlows call's entries are included.
        if (!services.Any(d => d.ServiceType == typeof(IDataFlowBlockMetadataStore)))
        {
            services.AddSingleton<IDataFlowBlockMetadataStore>(sp =>
                new DataFlowBlockMetadataStore(sp.GetServices<DataFlowBlockMetadataContribution>()));
        }

        return services;
    }
}
