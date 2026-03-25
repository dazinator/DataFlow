namespace DataFlow.Blazor.FlowMetadata;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering flow-level visualization metadata.
/// </summary>
public static class DataFlowFlowMetadataServiceExtensions
{
    /// <summary>
    /// Registers or updates the display name for a named dataflow in the shared
    /// <see cref="IDataFlowFlowMetadataStore"/> singleton.
    /// Intended for use by infrastructure code (e.g. <c>DataFlowBuilder</c>) during
    /// service registration.
    /// </summary>
    public static IServiceCollection RegisterFlowDisplayName(
        this IServiceCollection services,
        string flowName,
        string displayName)
    {
        DataFlowFlowMetadataRegistry.GetOrCreate(services)
            .Set(flowName, new DataFlowFlowMetadata { DisplayName = displayName });
        return services;
    }
}
