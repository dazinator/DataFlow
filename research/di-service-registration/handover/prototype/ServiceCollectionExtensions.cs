namespace DataFlow.POC.DependencyInjection;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder for registering DataFlow components with dependency injection.
/// Provides a fluent API for registering blocks, strategies, and other components by name.
/// </summary>
public class DataFlowBuilder
{
    private readonly IServiceCollection _services;

    internal DataFlowBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Register a block with a unique name/key for later resolution.
    /// The block will be registered as a keyed singleton service.
    /// </summary>
    /// <typeparam name="TBlock">The block type (must implement IBlock)</typeparam>
    /// <param name="name">Unique name/key for this block</param>
    /// <param name="factory">Factory function to create the block</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddBlock<TBlock>(string name, Func<IServiceProvider, TBlock> factory)
        where TBlock : IBlock
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Block name cannot be null or whitespace", nameof(name));
        
        ArgumentNullException.ThrowIfNull(factory);

        // Register as keyed service so we can resolve by name
        _services.AddKeyedSingleton<IBlock>(name, (sp, key) => factory(sp));
        
        return this;
    }

    /// <summary>
    /// Register a block instance with a unique name/key.
    /// Useful for simple blocks that don't need DI dependencies.
    /// </summary>
    /// <param name="name">Unique name/key for this block</param>
    /// <param name="block">The block instance</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddBlock(string name, IBlock block)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Block name cannot be null or whitespace", nameof(name));
        
        ArgumentNullException.ThrowIfNull(block);

        _services.AddKeyedSingleton<IBlock>(name, block);
        
        return this;
    }

    /// <summary>
    /// Register an edge strategy with a unique name/key for later resolution.
    /// The strategy will be registered as a keyed singleton service.
    /// </summary>
    /// <typeparam name="TStrategy">The strategy type (must inherit from EdgeStrategy)</typeparam>
    /// <param name="name">Unique name/key for this strategy</param>
    /// <param name="factory">Factory function to create the strategy</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddStrategy<TStrategy>(string name, Func<IServiceProvider, TStrategy> factory)
        where TStrategy : EdgeStrategy
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Strategy name cannot be null or whitespace", nameof(name));
        
        ArgumentNullException.ThrowIfNull(factory);

        _services.AddKeyedSingleton<EdgeStrategy>(name, (sp, key) => factory(sp));
        
        return this;
    }

    /// <summary>
    /// Register a strategy instance with a unique name/key.
    /// </summary>
    /// <param name="name">Unique name/key for this strategy</param>
    /// <param name="strategy">The strategy instance</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddStrategy(string name, EdgeStrategy strategy)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Strategy name cannot be null or whitespace", nameof(name));
        
        ArgumentNullException.ThrowIfNull(strategy);

        _services.AddKeyedSingleton<EdgeStrategy>(name, strategy);
        
        return this;
    }
}

/// <summary>
/// Extension methods for registering DataFlow components with IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add DataFlow components to the service collection.
    /// Provides a fluent API for registering blocks and strategies by name.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action for the DataFlow builder</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddDataFlows(df => 
    /// {
    ///     df.AddBlock("producer", sp => new ProducerBlock&lt;int&gt;(...));
    ///     df.AddBlock("transform", sp => new TransformBlock&lt;int, string&gt;(...));
    ///     df.AddStrategy("competing", sp => new CompetingEdgeStrategy(...));
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddDataFlows(
        this IServiceCollection services,
        Action<DataFlowBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new DataFlowBuilder(services);
        configure(builder);

        return services;
    }
}
