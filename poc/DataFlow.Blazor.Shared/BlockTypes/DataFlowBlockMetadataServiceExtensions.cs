namespace DataFlow.Blazor.BlockTypes;

using Microsoft.Extensions.DependencyInjection;

public static class DataFlowBlockMetadataServiceExtensions
{
    /// <summary>
    /// Registers an <see cref="IDataFlowBlockMetadataStore"/> singleton that maps block names
    /// to human-readable display names and type labels shown in the flow visualization.
    ///
    /// <example>
    /// <code>
    /// services.AddDataFlowBlockMetadata(blocks =>
    /// {
    ///     blocks.ForBlock("journal-v2:erp-poster-1")
    ///           .DisplayName("ERP Poster");
    ///
    ///     blocks.ForBlock("journal-v2:custom-step")
    ///           .DisplayName("Custom Step")
    ///           .TypeLabel("Custom Processor");
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public static IServiceCollection AddDataFlowBlockMetadata(
        this IServiceCollection services,
        Action<DataFlowBlockMetadataStoreBuilder> configure)
    {
        var builder = new DataFlowBlockMetadataStoreBuilder();
        configure(builder);
        services.AddSingleton<IDataFlowBlockMetadataStore>(builder.Build());
        return services;
    }
}
