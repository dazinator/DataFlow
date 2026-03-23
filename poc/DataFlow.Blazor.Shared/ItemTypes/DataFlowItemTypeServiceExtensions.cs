namespace DataFlow.Blazor.ItemTypes;

using Microsoft.Extensions.DependencyInjection;

public static class DataFlowItemTypeServiceExtensions
{
    /// <summary>
    /// Registers an <see cref="IDataFlowItemTypeStore"/> singleton that maps CLR item types
    /// to human-readable labels shown in the flow visualization.
    ///
    /// <example>
    /// <code>
    /// services.AddDataFlowItemTypes(items =>
    /// {
    ///     items.ForType&lt;Invoice&gt;().Label("Invoice");
    ///     items.ForType&lt;Invoice[]&gt;().Label("Invoice batch");
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public static IServiceCollection AddDataFlowItemTypes(
        this IServiceCollection services,
        Action<DataFlowItemTypeStoreBuilder> configure)
    {
        var builder = new DataFlowItemTypeStoreBuilder();
        configure(builder);
        services.AddSingleton<IDataFlowItemTypeStore>(builder.Build());
        return services;
    }
}
