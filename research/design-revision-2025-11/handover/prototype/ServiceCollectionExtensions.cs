namespace DataFlow.POC.DependencyInjection;

using DataFlow.POC.Core;
using DataFlow.POC.Builder;
using DataFlow.POC.Blocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Builder for registering DataFlow components with dependency injection.
/// Provides a fluent API for registering blocks, strategies, and graphs.
/// </summary>
public class DataFlowBuilder
{
    private readonly IServiceCollection _services;
    private readonly string _namespace;

    internal DataFlowBuilder(IServiceCollection services, string? namespacePrefix = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _namespace = namespacePrefix ?? "global";
    }

    /// <summary>
    /// Gets the namespace prefix used for this builder.
    /// Default is "global" if no namespace was specified.
    /// </summary>
    public string Namespace => _namespace;

    #region Block Registration

    /// <summary>
    /// Register a block as a scoped service with a unique name/key.
    /// This is the default and recommended lifetime for most blocks.
    /// </summary>
    /// <typeparam name="TBlock">The block type (must implement IBlock)</typeparam>
    /// <param name="name">Unique name/key for this block</param>
    /// <param name="factory">Factory function to create the block</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddBlock<TBlock>(string name, Func<IServiceProvider, TBlock> factory)
        where TBlock : IBlock
    {
        return AddScopedBlock(name, factory);
    }

    /// <summary>
    /// Register a block instance as a scoped service.
    /// Note: The same instance will be used within a scope, but different instances across scopes.
    /// For true singleton instances, use AddSingletonBlock.
    /// </summary>
    /// <param name="name">Unique name/key for this block</param>
    /// <param name="block">The block instance factory</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddBlock(string name, IBlock block)
    {
        return AddScopedBlock(name, sp => block);
    }

    /// <summary>
    /// Register a block as a scoped service.
    /// Scoped services are created once per scope (e.g., per graph execution).
    /// This is safe for blocks that use scoped dependencies like DbContext.
    /// </summary>
    public DataFlowBuilder AddScopedBlock<TBlock>(string name, Func<IServiceProvider, TBlock> factory)
        where TBlock : IBlock
    {
        ValidateBlockName(name);
        ArgumentNullException.ThrowIfNull(factory);
        
        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Block");

        _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) => factory(sp));
        return this;
    }

    /// <summary>
    /// Register a block as a singleton service.
    /// Use this for stateless blocks or blocks that are safe to share across the application.
    /// WARNING: Singleton blocks cannot use scoped dependencies.
    /// </summary>
    public DataFlowBuilder AddSingletonBlock<TBlock>(string name, Func<IServiceProvider, TBlock> factory)
        where TBlock : IBlock
    {
        ValidateBlockName(name);
        ArgumentNullException.ThrowIfNull(factory);
        
        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Block");

        _services.AddKeyedSingleton<IBlock>(fullKey, (sp, key) => factory(sp));
        return this;
    }

    /// <summary>
    /// Register a block as a transient service.
    /// A new instance will be created each time it is requested.
    /// Use this sparingly as it can impact performance.
    /// </summary>
    public DataFlowBuilder AddTransientBlock<TBlock>(string name, Func<IServiceProvider, TBlock> factory)
        where TBlock : IBlock
    {
        ValidateBlockName(name);
        ArgumentNullException.ThrowIfNull(factory);
        
        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Block");

        _services.AddKeyedTransient<IBlock>(fullKey, (sp, key) => factory(sp));
        return this;
    }

    #endregion

    #region Strategy Registration

    /// <summary>
    /// Register an edge strategy as a scoped service.
    /// This is the default lifetime for strategies.
    /// </summary>
    public DataFlowBuilder AddStrategy<TStrategy>(string name, Func<IServiceProvider, TStrategy> factory)
        where TStrategy : EdgeStrategy
    {
        return AddScopedStrategy(name, factory);
    }

    /// <summary>
    /// Register a strategy instance as a scoped service.
    /// </summary>
    public DataFlowBuilder AddStrategy(string name, EdgeStrategy strategy)
    {
        return AddScopedStrategy(name, sp => strategy);
    }

    /// <summary>
    /// Register a strategy as a scoped service.
    /// </summary>
    public DataFlowBuilder AddScopedStrategy<TStrategy>(string name, Func<IServiceProvider, TStrategy> factory)
        where TStrategy : EdgeStrategy
    {
        ValidateStrategyName(name);
        ArgumentNullException.ThrowIfNull(factory);
        
        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Strategy");

        _services.AddKeyedScoped<EdgeStrategy>(fullKey, (sp, key) => factory(sp));
        return this;
    }

    /// <summary>
    /// Register a strategy as a singleton service.
    /// </summary>
    public DataFlowBuilder AddSingletonStrategy<TStrategy>(string name, Func<IServiceProvider, TStrategy> factory)
        where TStrategy : EdgeStrategy
    {
        ValidateStrategyName(name);
        ArgumentNullException.ThrowIfNull(factory);
        
        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Strategy");

        _services.AddKeyedSingleton<EdgeStrategy>(fullKey, (sp, key) => factory(sp));
        return this;
    }

    #endregion

    #region Graph Registration

    /// <summary>
    /// Register a DataFlow graph with a specified topology.
    /// The graph will be built using the registered blocks and can be resolved from DI.
    /// </summary>
    /// <param name="name">Unique name for this graph</param>
    /// <param name="configure">Action to configure the graph topology</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddGraph(string name, Action<DataFlowGraphBuilder> configure)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Graph name cannot be null or whitespace", nameof(name));
        
        ArgumentNullException.ThrowIfNull(configure);
        
        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Graph");

        // Capture the namespace to pass to graph builder
        var currentNamespace = _namespace;
        
        _services.AddKeyedScoped<DataFlowGraph>(fullKey, (sp, key) =>
        {
            var builder = new DataFlowGraphBuilder(name, sp, currentNamespace);
            configure(builder);
            return builder.Build();
        });

        return this;
    }

    /// <summary>
    /// Register a DataFlow graph using a class-based definition.
    /// This is useful for complex graphs that benefit from organization into a separate class.
    /// </summary>
    /// <typeparam name="TDefinition">The graph definition type</typeparam>
    /// <param name="name">Unique name for this graph</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddGraphDefinition<TDefinition>(string name)
        where TDefinition : class, IDataFlowDefinition
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Graph name cannot be null or whitespace", nameof(name));
        
        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Graph");

        // Capture the namespace to pass to graph builder
        var currentNamespace = _namespace;

        // Register the definition class if not already registered
        _services.TryAddScoped<TDefinition>();

        _services.AddKeyedScoped<DataFlowGraph>(fullKey, (sp, key) =>
        {
            var definition = sp.GetRequiredService<TDefinition>();
            var builder = new DataFlowGraphBuilder(name, sp, currentNamespace);
            definition.Configure(builder);
            return builder.Build();
        });

        return this;
    }

    #endregion

    #region Typed Helper Methods (Problem 6: Block Name Duplication)

    /// <summary>
    /// Register an ActorBlock with type-safe API. No name duplication required.
    /// All dependencies are automatically injected via DI.
    /// Uses IBlockContext initialization pattern for proper lifecycle management.
    /// </summary>
    /// <typeparam name="TIn">Input type</typeparam>
    /// <typeparam name="TOut">Output type</typeparam>
    /// <typeparam name="TActor">Actor type</typeparam>
    /// <param name="name">Unique name for this block</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddActorBlock<TIn, TOut, TActor>(string name)
        where TActor : IStreamActor<TIn, TOut>
    {
        ValidateBlockName(name);
        
        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Block");

        // Register ActorBlock<TIn, TOut, TActor> as scoped for DI resolution
        _services.TryAddScoped<ActorBlock<TIn, TOut, TActor>>();

        _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
        {
            // Resolve block from DI (all dependencies auto-injected, scoped services tracked)
            var block = sp.GetRequiredService<ActorBlock<TIn, TOut, TActor>>();
            // Initialize block with context containing name
            var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
            var context = new BlockContext(blockName);
            block.SetContext(context);
            return block;
        });

        return this;
    }

    #endregion

    #region Validation Helpers

    private void ValidateBlockName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Block name cannot be null or whitespace", nameof(name));
    }

    private void ValidateStrategyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Strategy name cannot be null or whitespace", nameof(name));
    }

    /// <summary>
    /// Resolves the full key for a component by applying namespace prefix if needed.
    /// If the name already contains a colon (:), it's treated as a fully-qualified key.
    /// Otherwise, the current namespace prefix is applied.
    /// </summary>
    private string ResolveKey(string name)
    {
        // If name contains ':', treat it as fully-qualified (e.g., "global:producer" or "moduleA:transformer")
        if (name.Contains(':'))
        {
            return name;
        }
        
        // Apply current namespace prefix
        return $"{_namespace}:{name}";
    }

    private void CheckDuplicateRegistration(string fullKey, string componentType)
    {
        // Check for duplicate keyed service registration using the full key
        bool isDuplicate = _services.Any(sd => 
            sd.ServiceKey?.ToString() == fullKey && 
            (sd.ServiceType == typeof(IBlock) || 
             sd.ServiceType == typeof(EdgeStrategy) || 
             sd.ServiceType == typeof(DataFlowGraph)));

        if (isDuplicate)
        {
            throw new InvalidOperationException(
                $"{componentType} '{fullKey}' is already registered. " +
                $"Each block, strategy, and graph must have a unique name. " +
                $"If you intended to override the registration, remove the previous registration first.");
        }
    }

    #endregion
}

/// <summary>
/// Interface for class-based DataFlow graph definitions.
/// Implement this interface to define graph topology in a reusable, testable way.
/// </summary>
public interface IDataFlowDefinition
{
    /// <summary>
    /// Configure the graph topology using the provided builder.
    /// </summary>
    /// <param name="builder">The graph builder to configure</param>
    void Configure(DataFlowGraphBuilder builder);
}

/// <summary>
/// Extension methods for registering DataFlow components with IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add DataFlow components to the service collection in the global namespace.
    /// Components registered without a namespace prefix use "global" as the default namespace.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Configuration action for the DataFlow builder</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddDataFlows(df => 
    /// {
    ///     // Register blocks (scoped by default) - keys will be "global:producer", "global:transformer"
    ///     df.AddBlock("producer", sp => new ProducerBlock&lt;int&gt;(...));
    ///     df.AddBlock("transformer", sp => new TransformBlock&lt;int, string&gt;(...));
    ///     
    ///     // Register a graph - key will be "global:main"
    ///     df.AddGraph("main", g => 
    ///     {
    ///         g.UseBlock("producer")       // Resolves "global:producer"
    ///          .UseBlock("transformer")    // Resolves "global:transformer"
    ///          .Connect("producer", "transformer");
    ///     });
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddDataFlows(
        this IServiceCollection services,
        Action<DataFlowBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Use "global" as default namespace
        var builder = new DataFlowBuilder(services, namespacePrefix: null);
        configure(builder);

        return services;
    }

    /// <summary>
    /// Add DataFlow components to the service collection with a specific namespace prefix.
    /// This allows multiple modules to register components with the same logical names without conflicts.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="namespacePrefix">The namespace prefix for all components (e.g., "moduleA", "moduleB")</param>
    /// <param name="configure">Configuration action for the DataFlow builder</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// // Module A
    /// services.AddDataFlows("moduleA", df => 
    /// {
    ///     // Keys will be "moduleA:producer", "moduleA:transformer"
    ///     df.AddBlock("producer", sp => new ProducerBlock&lt;int&gt;(...));
    ///     df.AddBlock("transformer", sp => new ActorBlock&lt;...&gt;(...));
    ///     
    ///     // Graph key will be "moduleA:main"
    ///     df.AddGraph("main", g => 
    ///     {
    ///         g.UseBlock("producer")       // Resolves "moduleA:producer"
    ///          .UseBlock("transformer")    // Resolves "moduleA:transformer"
    ///          .UseBlock("global:logger")  // Can reference global namespace explicitly
    ///          .Connect("producer", "transformer");
    ///     });
    /// });
    /// 
    /// // Module B can use same logical names without conflict
    /// services.AddDataFlows("moduleB", df => 
    /// {
    ///     df.AddBlock("producer", sp => new ProducerBlock&lt;string&gt;(...)); // "moduleB:producer"
    ///     df.AddBlock("transformer", sp => new TransformBlock&lt;...&gt;(...)); // "moduleB:transformer"
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddDataFlows(
        this IServiceCollection services,
        string namespacePrefix,
        Action<DataFlowBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        
        if (string.IsNullOrWhiteSpace(namespacePrefix))
            throw new ArgumentException("Namespace prefix cannot be null or whitespace", nameof(namespacePrefix));
        
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new DataFlowBuilder(services, namespacePrefix);
        configure(builder);

        return services;
    }
}
