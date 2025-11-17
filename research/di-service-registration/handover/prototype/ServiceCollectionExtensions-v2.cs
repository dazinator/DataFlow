namespace DataFlow.POC.DependencyInjection;

using DataFlow.POC.Builder;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
/// Interface for class-based DataFlow definition.
/// Separates service registration from graph building for better organization.
/// </summary>
public interface IDataFlowDefinition
{
    /// <summary>
    /// Register DataFlow services (blocks, strategies, etc.) with the builder.
    /// </summary>
    void RegisterServices(DataFlowBuilder builder);
    
    /// <summary>
    /// Configure the DataFlow graph structure using registered services.
    /// </summary>
    void ConfigureGraph(DataFlowGraphBuilderEx builder);
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
    
    /// <summary>
    /// Add a class-based DataFlow definition to the service collection.
    /// The definition will be registered and available for building graphs.
    /// </summary>
    /// <typeparam name="TDefinition">The DataFlow definition type</typeparam>
    /// <param name="services">The service collection</param>
    /// <param name="name">Unique name for this DataFlow definition</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddDataFlowDefinition&lt;MyDataFlowDefinition&gt;("my-flow");
    /// 
    /// // Later, build the graph:
    /// var definition = serviceProvider.GetRequiredService&lt;MyDataFlowDefinition&gt;();
    /// var graphBuilder = new DataFlowGraphBuilderEx("my-flow", serviceProvider);
    /// definition.ConfigureGraph(graphBuilder);
    /// var graph = graphBuilder.Build();
    /// </code>
    /// </example>
    public static IServiceCollection AddDataFlowDefinition<TDefinition>(
        this IServiceCollection services,
        string name)
        where TDefinition : class, IDataFlowDefinition, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("DataFlow name cannot be null or whitespace", nameof(name));

        // Register the definition as singleton
        services.TryAddSingleton<TDefinition>();
        
        // Create temporary instance to let it register services
        var definition = new TDefinition();
        var builder = new DataFlowBuilder(services);
        definition.RegisterServices(builder);

        return services;
    }
    
    /// <summary>
    /// Add an isolated DataFlow with its own service collection.
    /// Returns a builder that must be completed by calling BuildIsolated().
    /// </summary>
    /// <param name="services">The main application service collection</param>
    /// <param name="name">Unique name for this isolated DataFlow</param>
    /// <returns>An isolated DataFlow builder</returns>
    /// <example>
    /// <code>
    /// services.AddIsolatedDataFlow("my-flow")
    ///     .ConfigureServices(df => 
    ///     {
    ///         df.AddBlock("producer", sp => new ProducerBlock&lt;int&gt;(...));
    ///     })
    ///     .ConfigureGraph((builder, sp) =>
    ///     {
    ///         builder.UseBlock("producer");
    ///     })
    ///     .BuildIsolated();
    /// </code>
    /// </example>
    public static IsolatedDataFlowBuilder AddIsolatedDataFlow(
        this IServiceCollection services,
        string name)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("DataFlow name cannot be null or whitespace", nameof(name));

        return new IsolatedDataFlowBuilder(services, name);
    }
}

/// <summary>
/// Builder for configuring an isolated DataFlow with its own service collection.
/// </summary>
public class IsolatedDataFlowBuilder
{
    private readonly IServiceCollection _mainServices;
    private readonly string _name;
    private readonly IServiceCollection _isolatedServices;
    private Action<DataFlowBuilder>? _configureServices;
    private Action<DataFlowGraphBuilderEx, IServiceProvider>? _configureGraph;

    internal IsolatedDataFlowBuilder(IServiceCollection mainServices, string name)
    {
        _mainServices = mainServices;
        _name = name;
        _isolatedServices = new ServiceCollection();
    }

    /// <summary>
    /// Configure the isolated service collection for this DataFlow.
    /// </summary>
    public IsolatedDataFlowBuilder ConfigureServices(Action<DataFlowBuilder> configure)
    {
        _configureServices = configure;
        return this;
    }

    /// <summary>
    /// Configure the graph structure using the isolated services.
    /// </summary>
    public IsolatedDataFlowBuilder ConfigureGraph(Action<DataFlowGraphBuilderEx, IServiceProvider> configure)
    {
        _configureGraph = configure;
        return this;
    }

    /// <summary>
    /// Build and register the isolated DataFlow.
    /// This registers a factory in the main service collection that creates DataFlow graphs
    /// using the isolated service provider.
    /// </summary>
    public IServiceCollection BuildIsolated()
    {
        // Configure isolated services if provided
        if (_configureServices != null)
        {
            var builder = new DataFlowBuilder(_isolatedServices);
            _configureServices(builder);
        }

        // Store the isolated services descriptor for later building
        _mainServices.AddKeyedSingleton<IServiceCollection>($"__isolated__{_name}", _isolatedServices);
        
        // Register factory that will build graphs using the isolated services
        _mainServices.AddKeyedSingleton<Func<IServiceProvider, DataFlowGraph>>(_name, (mainSp, key) =>
        {
            return (isolatedSp) =>
            {
                var graphBuilder = new DataFlowGraphBuilderEx(_name, isolatedSp);
                _configureGraph?.Invoke(graphBuilder, isolatedSp);
                return graphBuilder.Build();
            };
        });

        return _mainServices;
    }
}
