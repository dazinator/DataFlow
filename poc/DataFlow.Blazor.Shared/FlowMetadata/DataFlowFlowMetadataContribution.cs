namespace DataFlow.Blazor.FlowMetadata;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Carries the flow-level visualization metadata contributed by a single <c>AddDataFlows()</c> call.
/// Multiple instances are registered as singletons and aggregated by
/// <see cref="DataFlowFlowMetadataStore"/> at first resolution using the standard
/// <see cref="IEnumerable{T}"/> DI pattern.
///
/// This type is part of the infrastructure used by <c>DataFlowBuilder</c> and is not
/// intended for direct use by application code. Configure flow metadata using the
/// <c>DisplayName</c> method on the builder:
/// <code>
/// services.AddDataFlows("journal-v2", df =>
/// {
///     df.DisplayName("Journal Processing Flow");
/// });
/// </code>
/// </summary>
public sealed class DataFlowFlowMetadataContribution
{
    /// <summary>The flow's registered namespace key (e.g. <c>"journal-v2"</c>).</summary>
    public string FlowName { get; }

    /// <summary>Visualization metadata for the flow.</summary>
    public DataFlowFlowMetadata Metadata { get; }

    /// <param name="flowName">The flow's registered namespace key.</param>
    /// <param name="metadata">Metadata configured for this flow.</param>
    public DataFlowFlowMetadataContribution(string flowName, DataFlowFlowMetadata metadata)
    {
        FlowName = flowName;
        Metadata = metadata;
    }

    /// <summary>
    /// Registers a flow display name as a contribution and ensures the aggregating
    /// <see cref="IDataFlowFlowMetadataStore"/> singleton is registered exactly once.
    /// Called by <c>DataFlowBuilder</c> — not intended for direct use.
    /// </summary>
    public static IServiceCollection Register(
        IServiceCollection services,
        string flowName,
        string displayName)
    {
        services.AddSingleton(new DataFlowFlowMetadataContribution(
            flowName, new DataFlowFlowMetadata { DisplayName = displayName }));

        // Register the aggregating store only once.
        if (!services.Any(d => d.ServiceType == typeof(IDataFlowFlowMetadataStore)))
        {
            services.AddSingleton<IDataFlowFlowMetadataStore>(sp =>
                new DataFlowFlowMetadataStore(sp.GetServices<DataFlowFlowMetadataContribution>()));
        }

        return services;
    }
}
