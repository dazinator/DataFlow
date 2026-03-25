namespace DataFlow.POC.DependencyInjection;

using DataFlow.Blazor.BlockTypes;
using DataFlow.Blazor.FlowMetadata;
using DataFlow.POC.Core;
using DataFlow.POC.Builder;
using DataFlow.POC.Blocks;
using DataFlow.POC.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Trace;

/// <summary>
/// Builder for registering DataFlow components with dependency injection.
/// Provides a fluent API for registering blocks, strategies, and graphs.
/// </summary>
public class DataFlowBuilder
{
    private readonly IServiceCollection _services;
    private readonly IBlockTypeRegistry _registry;
    private readonly DataFlowBlockMetadataStoreBuilder _blockMetadataBuilder = new();
    private const string DefaultNamespacePrefix = "global";
    private string? _flowDisplayName;

    internal DataFlowBuilder(IServiceCollection services, IBlockTypeRegistry registry, string? namespacePrefix = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Namespace = namespacePrefix ?? DefaultNamespacePrefix;
    }

    /// <summary>
    /// Gets the namespace prefix used for this builder.
    /// Default is "global" if no namespace was specified.
    /// </summary>
    public string Namespace { get; }

    /// <summary>
    /// Sets a human-readable display name for the dataflow registered under this builder's
    /// namespace. The name is stored in <see cref="IDataFlowFlowMetadataStore"/> and can be
    /// surfaced in the visualization instead of the raw namespace key.
    ///
    /// <example>
    /// <code>
    /// services.AddDataFlows("journal-v2", df =>
    /// {
    ///     df.DisplayName("Journal Processing Flow");
    ///     df.AddActorBlock&lt;Journal, JournalBatch, BatchActor&gt;("batcher");
    ///     // …
    /// });
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="displayName">Human-readable name for this flow.</param>
    /// <returns>This builder for chaining.</returns>
    public DataFlowBuilder DisplayName(string displayName)
    {
        _flowDisplayName = displayName;
        return this;
    }

    /// <summary>
    /// Fluent API for setting visualization metadata on a block at registration time.
    /// Obtained from the callback parameter of <c>AddBlock</c>, <c>AddActorBlock</c>, etc.
    /// </summary>
    public interface IBlockMetadataBuilder
    {
        /// <summary>Sets the human-readable display name shown as the block title in the visualization.</summary>
        IBlockMetadataBuilder DisplayName(string displayName);

        /// <summary>
        /// Overrides the block type label shown beneath the title in the visualization.
        /// When not set, <see cref="DataFlowBlockTypeNameFormatter.Format"/> provides the default.
        /// </summary>
        IBlockMetadataBuilder TypeLabel(string typeLabel);
    }

    private sealed class BlockMetadataBuilderAdapter : IBlockMetadataBuilder
    {
        private readonly DataFlowBlockMetadataStoreBuilder.IBlockMetadataBuilder _inner;

        internal BlockMetadataBuilderAdapter(DataFlowBlockMetadataStoreBuilder.IBlockMetadataBuilder inner)
            => _inner = inner;

        public IBlockMetadataBuilder DisplayName(string displayName)
        {
            _inner.DisplayName(displayName);
            return this;
        }

        public IBlockMetadataBuilder TypeLabel(string typeLabel)
        {
            _inner.TypeLabel(typeLabel);
            return this;
        }
    }

    private void ApplyBlockMetadata(string fullKey, Action<IBlockMetadataBuilder>? configure)
    {
        if (configure is null) return;
        var inner = _blockMetadataBuilder.ForBlock(fullKey);
        configure(new BlockMetadataBuilderAdapter(inner));
    }

    internal void RegisterCollectedMetadata(IServiceCollection services)
    {
        services.MergeBlockMetadata(_blockMetadataBuilder.GetEntries());

        if (_flowDisplayName is not null)
            services.RegisterFlowDisplayName(Namespace, _flowDisplayName);
    }

    #region Block Registration

    /// <summary>
    /// Register a block as a scoped service with a unique name/key.
    /// This is the default and recommended lifetime for most blocks.
    /// </summary>
    /// <typeparam name="TBlock">The block type (must implement IBlock)</typeparam>
    /// <param name="name">Unique name/key for this block</param>
    /// <param name="factory">Factory function to create the block</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddBlock<TBlock>(string name, Func<IServiceProvider, TBlock> factory,
        Action<IBlockMetadataBuilder>? configure = null)
        where TBlock : IBlock
    {
        return AddScopedBlock(name, factory, configure);
    }

    /// <summary>
    /// Register a block instance as a scoped service.
    /// Note: The same instance will be used within a scope, but different instances across scopes.
    /// Only scoped lifetime is supported for blocks.
    /// </summary>
    /// <param name="name">Unique name/key for this block</param>
    /// <param name="block">The block instance factory</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddBlock(string name, IBlock block,
        Action<IBlockMetadataBuilder>? configure = null)
    {
        return AddScopedBlock(name, sp => block, configure);
    }

    /// <summary>
    /// Register a block as a scoped service.
    /// Scoped services are created once per scope (e.g., per graph execution).
    /// This is safe for blocks that use scoped dependencies like DbContext.
    /// 
    /// Note: For introspection to work before first block resolution, type information
    /// must be extractable via reflection from TBlock implementing IBlock&lt;TIn, TOut&gt;.
    /// If your block type doesn't implement the generic interface directly, use
    /// AddActorBlock or provide metadata explicitly via overload (if available).
    /// </summary>
    public DataFlowBuilder AddScopedBlock<TBlock>(string name, Func<IServiceProvider, TBlock> factory,
        Action<IBlockMetadataBuilder>? configure = null)
        where TBlock : IBlock
    {
        ValidateBlockName(name);
        ArgumentNullException.ThrowIfNull(factory);

        var fullKey = ResolveKey(name);
        ApplyBlockMetadata(fullKey, configure);
        CheckDuplicateRegistration(fullKey, "Block");

        // Try to extract type parameters from IBlock<TIn, TOut> interface for eager metadata registration
        Type? inputType = null;
        Type? outputType = null;
        
        var blockType = typeof(TBlock);
        
        // Check if TBlock itself is the generic IBlock<,> (for direct interface implementations)
        Type? genericBlockInterface = null;
        if (blockType.IsGenericType && blockType.GetGenericTypeDefinition() == typeof(IBlock<,>))
        {
            genericBlockInterface = blockType;
        }
        else
        {
            // Otherwise search in implemented interfaces
            genericBlockInterface = blockType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBlock<,>));
        }
        
        if (genericBlockInterface != null)
        {
            var typeArgs = genericBlockInterface.GetGenericArguments();
            inputType = typeArgs[0];
            outputType = typeArgs[1];
            
            // Register metadata eagerly for introspection before first resolution
            var metadata = new BlockTypeMetadata(inputType, outputType);
            _registry.RegisterBlock(fullKey, metadata);
        }
        // else: Type info not extractable via reflection - metadata will be registered on first resolution
        // This is a limitation for introspection - consider using AddActorBlock<TIn,TOut,TActor> for typed blocks

        // Register the block with DI
        _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
        {
            var block = factory(sp);
            
            // Lazy fallback: Register metadata on first resolution if not already registered
            // This handles cases where reflection couldn't extract type info above
            // Use TryRegisterBlock to avoid race condition on concurrent first resolution
            var metadata = new BlockTypeMetadata(block.InputType, block.OutputType);
            _registry.TryRegisterBlock(fullKey, metadata);
            
            return block;
        });
        
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
        {
            throw new ArgumentException("Graph name cannot be null or whitespace", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(configure);
        
        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Graph");

        // Capture the namespace to pass to graph builder
        var currentNamespace = Namespace;
        
        _services.AddKeyedScoped<DataFlowGraph>(fullKey, (sp, key) =>
        {
            var registry = sp.GetRequiredService<IBlockTypeRegistry>();
            var builder = new DataFlowGraphBuilder(name, currentNamespace);
            configure(builder);
            return builder.Build(sp, registry);
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
        {
            throw new ArgumentException("Graph name cannot be null or whitespace", nameof(name));
        }

        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Graph");

        // Capture the namespace to pass to graph builder
        var currentNamespace = Namespace;

        // Register the definition class if not already registered
        _services.TryAddScoped<TDefinition>();

        _services.AddKeyedScoped<DataFlowGraph>(fullKey, (sp, key) =>
        {
            var definition = sp.GetRequiredService<TDefinition>();
            var registry = sp.GetRequiredService<IBlockTypeRegistry>();
            var builder = new DataFlowGraphBuilder(name, currentNamespace);
            definition.Configure(builder);
            return builder.Build(sp, registry);
        });

        return this;
    }

    #endregion

    #region Typed Helper Methods (Problem 6: Block Name Duplication)

    /// <summary>
    /// Register an EpochActorBlock with type-safe API. No name duplication required.
    /// All dependencies are automatically injected via constructor.
    /// Uses IBlockContext constructor injection for proper lifecycle management.
    /// 
    /// Note: This method now registers EpochActorBlock (epoch-aware processing).
    /// The plain ActorBlock variant has been removed in favor of unified epoch architecture.
    /// 
    /// The registry stores the semantic data types (TIn, TOut) that the actor processes,
    /// not the infrastructure wrapper types (IEpochStream<TIn>, IEpochStream<TOut>).
    /// This keeps the registry focused on the logical data contract, not implementation details.
    /// </summary>
    /// <typeparam name="TIn">Input type (plain data type, not epoch stream)</typeparam>
    /// <typeparam name="TOut">Output type (plain data type, not epoch stream)</typeparam>
    /// <typeparam name="TActor">Actor type</typeparam>
    /// <param name="name">Unique name for this block</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddActorBlock<TIn, TOut, TActor>(string name,
        Action<IBlockMetadataBuilder>? configure = null)
        where TActor : IStreamActor<TIn, TOut>
    {
        ValidateBlockName(name);

        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Block");
        ApplyBlockMetadata(fullKey, configure);

        // Register metadata with SEMANTIC types (what the actor actually processes)
        // NOT the infrastructure wrapper types (IEpochStream<>)
        // The wrapper is an implementation detail that connection validation should handle
        var metadata = new BlockTypeMetadata(typeof(TIn), typeof(TOut));
        _registry.RegisterBlock(fullKey, metadata);

        _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
        {
            // Step 1: Create context from key
            var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
            var context = new BlockContext(blockName);
            
            // Step 2: Resolve other dependencies
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            
            // Step 3: Construct epoch-aware block with ALL dependencies via constructor
            return new EpochActorBlock<TIn, TOut, TActor>(context, scopeFactory);
        });

        return this;
    }

    /// <summary>
    /// Register an <see cref="EpochBatchBlock{T}"/> with type-safe API.
    /// Batches incoming items into arrays within each epoch boundary.
    /// A new batch is emitted when it reaches <paramref name="maxBatchSize"/> items.
    /// When <paramref name="windowPeriod"/> is specified, the window timer starts on the first item
    /// in a batch; the batch is flushed on the next item arrival after the window expires (not
    /// proactively without input). Any remaining items at the end of an epoch are also emitted.
    ///
    /// Example usage:
    /// <code>
    /// df.AddBatch&lt;Journal&gt;("journal-batcher", maxBatchSize: 200, windowPeriod: TimeSpan.FromSeconds(2));
    ///
    /// df.AddGraph("flow", g =>
    /// {
    ///     g.UseBlock("journal-producer")
    ///      .UseBlock("journal-batcher")
    ///      .UseBlock("journal-processor");
    ///     g.Connect("journal-producer", "journal-batcher");
    ///     g.Connect("journal-batcher", "journal-processor");
    /// });
    /// </code>
    ///
    /// The registry stores the semantic data types (input: <typeparamref name="T"/>, output:
    /// <typeparamref name="T"/>[]) so connection type-validation works correctly.
    /// </summary>
    /// <typeparam name="T">Item type (input items; output will be <typeparamref name="T"/>[])</typeparam>
    /// <param name="name">Unique name for this batch block</param>
    /// <param name="maxBatchSize">Maximum number of items per batch (must be &gt; 0)</param>
    /// <param name="windowPeriod">
    /// Optional time window that triggers a batch flush on the next arriving item after the window
    /// elapses. Must be a positive duration when provided. When <see langword="null"/> only
    /// <paramref name="maxBatchSize"/> governs when a batch is flushed.
    /// </param>
    /// <returns>This builder for chaining</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown at registration time when <paramref name="maxBatchSize"/> is &lt;= 0, or when
    /// <paramref name="windowPeriod"/> is provided but is not a positive duration.
    /// </exception>
    public DataFlowBuilder AddBatch<T>(string name, int maxBatchSize, TimeSpan? windowPeriod = null,
        Action<IBlockMetadataBuilder>? configure = null)
    {
        ValidateBlockName(name);

        if (maxBatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBatchSize), maxBatchSize, "Max batch size must be greater than 0");
        }

        if (windowPeriod.HasValue && windowPeriod.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(windowPeriod), windowPeriod, "Window period must be a positive duration");
        }

        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Block");
        ApplyBlockMetadata(fullKey, configure);

        // Register metadata with SEMANTIC types (input: T, output: T[])
        // NOT the infrastructure wrapper types (IEpochStream<>)
        var metadata = new BlockTypeMetadata(typeof(T), typeof(T[]));
        _registry.RegisterBlock(fullKey, metadata);

        _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
        {
            // Step 1: Create context from key
            var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
            var context = new BlockContext(blockName);

            // Step 2: Construct batch block - no additional DI dependencies needed
            return new EpochBatchBlock<T>(context, maxBatchSize, windowPeriod);
        });

        return this;
    }

    /// <summary>
    /// Register a <see cref="RateLimitBlock{T}"/> using a pre-constructed
    /// <see cref="System.Threading.RateLimiting.RateLimiter"/> instance.
    /// The block is a pure pass-through (<typeparamref name="T"/> → <typeparamref name="T"/>) that
    /// throttles throughput by acquiring one permit per item before forwarding it downstream.
    ///
    /// <para>
    /// <b>Ownership:</b> The caller retains ownership of <paramref name="rateLimiter"/> and is
    /// responsible for disposing it. The block will <em>not</em> dispose the limiter when the
    /// block scope ends. This allows the same limiter instance to be shared safely across multiple
    /// graph executions and DI scopes.
    /// </para>
    ///
    /// Example usage:
    /// <code>
    /// // Register once; the limiter is owned and disposed by the caller
    /// using var limiter = new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
    /// {
    ///     PermitLimit = 2,
    ///     Window = TimeSpan.FromSeconds(1),
    ///     QueueLimit = int.MaxValue,
    /// });
    /// df.AddRateLimit&lt;Journal[]&gt;("rate-limiter", limiter);
    ///
    /// df.AddGraph("flow", g =>
    /// {
    ///     g.UseBlock("journal-batcher")
    ///      .UseBlock("rate-limiter")
    ///      .UseBlock("routing-transformer");
    ///     g.Connect("journal-batcher", "rate-limiter");
    ///     g.Connect("rate-limiter", "routing-transformer");
    /// });
    /// </code>
    /// </summary>
    /// <typeparam name="T">Item type flowing through the rate-limit block.</typeparam>
    /// <param name="name">Unique name for this block.</param>
    /// <param name="rateLimiter">
    /// The rate limiter instance to use. The caller retains ownership; this instance will
    /// <em>not</em> be disposed when the block or its DI scope is disposed.
    /// </param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rateLimiter"/> is <see langword="null"/>.</exception>
    public DataFlowBuilder AddRateLimit<T>(string name, System.Threading.RateLimiting.RateLimiter rateLimiter,
        Action<IBlockMetadataBuilder>? configure = null)
    {
        ValidateBlockName(name);
        ArgumentNullException.ThrowIfNull(rateLimiter);

        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Block");
        ApplyBlockMetadata(fullKey, configure);

        // Register metadata with SEMANTIC types (input: T, output: T)
        var metadata = new BlockTypeMetadata(typeof(T), typeof(T));
        _registry.RegisterBlock(fullKey, metadata);

        _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
        {
            var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
            var context = new BlockContext(blockName);
            // ownsLimiter: false — the caller supplied this instance and retains disposal responsibility.
            // This prevents the scoped block from disposing a shared limiter that must survive
            // across multiple graph executions / DI scopes.
            return new RateLimitBlock<T>(context, rateLimiter, ownsLimiter: false);
        });

        return this;
    }

    /// <summary>
    /// Register a <see cref="RateLimitBlock{T}"/> backed by a
    /// <see cref="System.Threading.RateLimiting.FixedWindowRateLimiter"/>.
    /// The block is a pure pass-through (<typeparamref name="T"/> → <typeparamref name="T"/>) that
    /// throttles throughput to <paramref name="permitLimit"/> items per <paramref name="window"/>.
    ///
    /// Example usage:
    /// <code>
    /// df.AddRateLimit&lt;Journal[]&gt;("rate-limiter", permitLimit: 2, window: TimeSpan.FromSeconds(1));
    ///
    /// df.AddGraph("flow", g =>
    /// {
    ///     g.UseBlock("journal-batcher")
    ///      .UseBlock("rate-limiter")
    ///      .UseBlock("routing-transformer");
    ///     g.Connect("journal-batcher", "rate-limiter");
    ///     g.Connect("rate-limiter", "routing-transformer");
    /// });
    /// </code>
    ///
    /// The registry stores the semantic data types (input: <typeparamref name="T"/>, output:
    /// <typeparamref name="T"/>) so connection type-validation works correctly.
    /// </summary>
    /// <typeparam name="T">Item type flowing through the rate-limit block.</typeparam>
    /// <param name="name">Unique name for this block.</param>
    /// <param name="permitLimit">Maximum number of items permitted per window (must be &gt; 0).</param>
    /// <param name="window">Duration of each rate-limit window (must be a positive duration).</param>
    /// <param name="queueLimit">
    /// Maximum number of items that may queue waiting for a permit.
    /// Defaults to <see cref="int.MaxValue"/> (unbounded queue).
    /// </param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown at registration time when <paramref name="permitLimit"/> is &lt;= 0,
    /// <paramref name="window"/> is not a positive duration, or <paramref name="queueLimit"/>
    /// is negative.
    /// </exception>
    public DataFlowBuilder AddRateLimit<T>(
        string name,
        int permitLimit,
        TimeSpan window,
        int queueLimit = int.MaxValue,
        Action<IBlockMetadataBuilder>? configure = null)
    {
        ValidateBlockName(name);

        if (permitLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(permitLimit), permitLimit, "Permit limit must be greater than 0");
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be a positive duration");
        }

        if (queueLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queueLimit), queueLimit, "Queue limit must be 0 or greater");
        }

        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Block");
        ApplyBlockMetadata(fullKey, configure);

        // Register metadata with SEMANTIC types (input: T, output: T)
        var metadata = new BlockTypeMetadata(typeof(T), typeof(T));
        _registry.RegisterBlock(fullKey, metadata);

        _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
        {
            var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
            var context = new BlockContext(blockName);
            var limiter = new System.Threading.RateLimiting.FixedWindowRateLimiter(
                new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = queueLimit,
                    QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                });
            return new RateLimitBlock<T>(context, limiter);
        });

        return this;
    }

    /// <summary>
    /// Register a <see cref="RateLimitBlock{T}"/> backed by a
    /// <see cref="System.Threading.RateLimiting.SlidingWindowRateLimiter"/>.
    /// The block is a pure pass-through (<typeparamref name="T"/> → <typeparamref name="T"/>) that
    /// throttles throughput using a sliding-window policy.
    ///
    /// Example usage:
    /// <code>
    /// df.AddSlidingWindowRateLimit&lt;Journal[]&gt;(
    ///     "rate-limiter",
    ///     permitLimit: 10,
    ///     window: TimeSpan.FromSeconds(1),
    ///     segmentsPerWindow: 5);
    /// </code>
    ///
    /// The registry stores the semantic data types (input: <typeparamref name="T"/>, output:
    /// <typeparamref name="T"/>) so connection type-validation works correctly.
    /// </summary>
    /// <typeparam name="T">Item type flowing through the rate-limit block.</typeparam>
    /// <param name="name">Unique name for this block.</param>
    /// <param name="permitLimit">Maximum number of items permitted per window (must be &gt; 0).</param>
    /// <param name="window">Duration of the sliding window (must be a positive duration).</param>
    /// <param name="segmentsPerWindow">Number of segments the window is divided into (must be &gt; 0).</param>
    /// <param name="queueLimit">
    /// Maximum number of items that may queue waiting for a permit.
    /// Defaults to <see cref="int.MaxValue"/> (unbounded queue).
    /// </param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown at registration time when <paramref name="permitLimit"/> or
    /// <paramref name="segmentsPerWindow"/> is &lt;= 0, <paramref name="window"/>
    /// is not a positive duration, or <paramref name="queueLimit"/> is negative.
    /// </exception>
    public DataFlowBuilder AddSlidingWindowRateLimit<T>(
        string name,
        int permitLimit,
        TimeSpan window,
        int segmentsPerWindow = 1,
        int queueLimit = int.MaxValue,
        Action<IBlockMetadataBuilder>? configure = null)
    {
        ValidateBlockName(name);

        if (permitLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(permitLimit), permitLimit, "Permit limit must be greater than 0");
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "Window must be a positive duration");
        }

        if (segmentsPerWindow <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentsPerWindow), segmentsPerWindow, "Segments per window must be greater than 0");
        }

        if (queueLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(queueLimit), queueLimit, "Queue limit must be 0 or greater");
        }

        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Block");
        ApplyBlockMetadata(fullKey, configure);

        // Register metadata with SEMANTIC types (input: T, output: T)
        var metadata = new BlockTypeMetadata(typeof(T), typeof(T));
        _registry.RegisterBlock(fullKey, metadata);

        _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
        {
            var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
            var context = new BlockContext(blockName);
            var limiter = new System.Threading.RateLimiting.SlidingWindowRateLimiter(
                new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    SegmentsPerWindow = segmentsPerWindow,
                    QueueLimit = queueLimit,
                    QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                });
            return new RateLimitBlock<T>(context, limiter);
        });

        return this;
    }

    /// <summary>
    /// Register an EpochSourceBlock with type-safe API.
    /// All dependencies are automatically injected via constructor.
    /// Uses IBlockContext constructor injection for proper lifecycle management.
    /// 
    /// Source blocks produce data streams with epoch boundaries without requiring input.
    /// The coordinator and scope factory are automatically resolved from DI.
    /// 
    /// The registry stores the semantic data types (input: object for no input, output: T)
    /// that the source produces, not the infrastructure wrapper types (IEpochStream&lt;T&gt;).
    /// This keeps the registry focused on the logical data contract, not implementation details.
    /// This matches how AddActorBlock registers semantic types for introspection.
    /// </summary>
    /// <typeparam name="T">Output type (data items produced by the source)</typeparam>
    /// <typeparam name="TActor">Source actor type implementing ISourceActor&lt;T&gt;</typeparam>
    /// <param name="name">Unique name for this source block</param>
    /// <returns>This builder for chaining</returns>
    public DataFlowBuilder AddSourceBlock<T, TActor>(string name,
        Action<IBlockMetadataBuilder>? configure = null)
        where TActor : ISourceActor<T>
    {
        ValidateBlockName(name);

        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Block");
        ApplyBlockMetadata(fullKey, configure);

        // Register metadata with SEMANTIC types (what the source produces)
        // Input type is 'object' since sources don't take input
        // Output type is T (the items produced), NOT IEpochStream<T> which is an infrastructure detail
        // This matches how AddActorBlock registers semantic types for introspection
        var metadata = new BlockTypeMetadata(typeof(object), typeof(T));
        _registry.RegisterBlock(fullKey, metadata);

        _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
        {
            // Step 1: Create context from key
            var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
            var context = new BlockContext(blockName);
            
            // Step 2: Resolve other dependencies
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            var coordinator = sp.GetRequiredService<IEpochCoordinator>();
            
            // Step 3: Construct epoch source block with ALL dependencies via constructor
            return new EpochSourceBlock<T, TActor>(context, scopeFactory, coordinator);
        });

        return this;
    }

    /// <summary>
    /// Register an <see cref="EpochBufferBlock{T}"/> with type-safe API.
    /// Buffers items within each epoch boundary, preserving epoch structure while
    /// decoupling producer and consumer processing rates.
    ///
    /// <para>
    /// <b>Capacity:</b> The capacity is per-epoch. When an epoch's buffer fills up,
    /// upstream producers will block until space becomes available, providing natural
    /// backpressure.
    /// </para>
    ///
    /// Example usage:
    /// <code>
    /// df.AddEpochBuffer&lt;Journal[]&gt;("erp-buffer", capacity: 50);
    ///
    /// df.AddGraph("flow", g =>
    /// {
    ///     g.UseBlock("router")
    ///      .UseBlock("erp-buffer")
    ///      .UseBlock("erp-poster");
    ///     g.Connect("router", "erp-buffer");
    ///     g.Connect("erp-buffer", "erp-poster");
    /// });
    /// </code>
    ///
    /// The registry stores the semantic data types (input: <typeparamref name="T"/>, output:
    /// <typeparamref name="T"/>) so connection type-validation works correctly.
    /// </summary>
    /// <typeparam name="T">Item type flowing through the buffer block.</typeparam>
    /// <param name="name">Unique name for this block.</param>
    /// <param name="capacity">Maximum number of items to buffer per epoch (must be &gt; 0).</param>
    /// <param name="configure">
    /// Optional callback to set visualization metadata (display name, type label) for this block.
    /// </param>
    /// <returns>This builder for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown at registration time when <paramref name="capacity"/> is &lt;= 0.
    /// </exception>
    public DataFlowBuilder AddEpochBuffer<T>(string name, int capacity,
        Action<IBlockMetadataBuilder>? configure = null)
    {
        ValidateBlockName(name);

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than 0");
        }

        var fullKey = ResolveKey(name);
        CheckDuplicateRegistration(fullKey, "Block");
        ApplyBlockMetadata(fullKey, configure);

        // Register metadata with SEMANTIC types (input: T, output: T)
        var metadata = new BlockTypeMetadata(typeof(T), typeof(T));
        _registry.RegisterBlock(fullKey, metadata);

        _services.AddKeyedScoped<IBlock>(fullKey, (sp, key) =>
        {
            var blockName = key as string ?? throw new InvalidOperationException("Block key must be a string");
            var context = new BlockContext(blockName);
            return new EpochBufferBlock<T>(context, new BufferConfiguration(capacity));
        });

        return this;
    }

    #endregion

    #region Validation Helpers

    private void ValidateBlockName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Block name cannot be null or whitespace", nameof(name));
        }
    }

    private void ValidateStrategyName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Strategy name cannot be null or whitespace", nameof(name));
        }
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
        return $"{Namespace}:{name}";
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
    /// Add DataFlow components to the service collection with a specific namespace prefix.
    /// This allows multiple modules to register components with the same logical names without conflicts.
    /// All components must be registered with an explicit namespace.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="namespacePrefix">The namespace prefix for all components (e.g., "global", "moduleA", "moduleB")</param>
    /// <param name="configure">Configuration action for the DataFlow builder</param>
    /// <returns>The service collection for chaining</returns>
    /// <example>
    /// <code>
    /// // Global namespace
    /// services.AddDataFlows("global", df => 
    /// {
    ///     // Keys will be "global:producer", "global:transformer"
    ///     df.AddBlock("producer", sp => new ProducerBlock&lt;int&gt;(...));
    ///     df.AddBlock("transformer", sp => new TransformBlock&lt;int, string&gt;(...));
    ///     
    ///     // Graph key will be "global:main"
    ///     df.AddGraph("main", g => 
    ///     {
    ///         g.UseBlock("producer")       // Resolves "global:producer"
    ///          .UseBlock("transformer")    // Resolves "global:transformer"
    ///          .Connect("producer", "transformer");
    ///     });
    /// });
    /// 
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
        {
            throw new ArgumentException("Namespace prefix cannot be null or whitespace", nameof(namespacePrefix));
        }

        ArgumentNullException.ThrowIfNull(configure);

        // Get or create the singleton registry instance
        // Use Any() with early exit for efficiency instead of FirstOrDefault
        IBlockTypeRegistry registry;
        var hasRegistry = services.Any(d => 
            d.ServiceType == typeof(IBlockTypeRegistry) && 
            d.Lifetime == ServiceLifetime.Singleton &&
            d.ImplementationInstance != null);
        
        if (hasRegistry)
        {
            // Find and reuse existing instance (only called when hasRegistry is true)
            var existingDescriptor = services.First(d => 
                d.ServiceType == typeof(IBlockTypeRegistry) && 
                d.ImplementationInstance != null);
            registry = (IBlockTypeRegistry)existingDescriptor.ImplementationInstance!;
        }
        else
        {
            // Create new instance and register it
            registry = new BlockTypeRegistry();
            services.AddSingleton<IBlockTypeRegistry>(registry);
        }

        // Auto-register IEpochCoordinator if not already registered
        // This is required infrastructure for epoch-based source blocks
        // Registered as SCOPED to isolate epoch coordination per graph execution
        services.TryAddScoped<IEpochCoordinator>(sp =>
            new EpochCoordinator(sp.GetRequiredService<IServiceScopeFactory>()));

        var builder = new DataFlowBuilder(services, registry, namespacePrefix);
        configure(builder);
        builder.RegisterCollectedMetadata(services);

        return services;
    }
}
