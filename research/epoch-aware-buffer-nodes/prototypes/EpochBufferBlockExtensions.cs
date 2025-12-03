namespace DataFlow.POC.Core;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// PROTOTYPE: Extension methods for adding epoch buffer blocks to the graph.
/// This is a research prototype to validate the API design.
/// </summary>
public static class EpochBufferBlockExtensions
{
    /// <summary>
    /// Adds an epoch-aware buffer block to the graph.
    /// The buffer preserves epoch boundaries while buffering items.
    /// </summary>
    /// <typeparam name="T">The type of items to buffer</typeparam>
    /// <param name="builder">The graph builder</param>
    /// <param name="name">Unique name for the buffer block</param>
    /// <param name="capacity">Maximum number of items to buffer per epoch</param>
    /// <param name="configureServices">Optional additional service configuration</param>
    /// <returns>The builder for method chaining</returns>
    public static DataFlowGraphBuilder AddEpochBuffer<T>(
        this DataFlowGraphBuilder builder,
        string name,
        int capacity,
        Action<IServiceCollection>? configureServices = null)
    {
        if (capacity <= 0)
        {
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));
        }

        return builder.AddBlock<EpochBufferBlock<T>>(
            name,
            services =>
            {
                // Register buffer configuration
                services.AddTransient<IBufferConfiguration>(sp =>
                    new BufferConfiguration(capacity));

                // Allow caller to register additional services
                configureServices?.Invoke(services);
            });
    }

    /// <summary>
    /// Adds an epoch-aware buffer block with custom configuration.
    /// </summary>
    /// <typeparam name="T">The type of items to buffer</typeparam>
    /// <param name="builder">The graph builder</param>
    /// <param name="name">Unique name for the buffer block</param>
    /// <param name="bufferConfiguration">Custom buffer configuration factory</param>
    /// <param name="configureServices">Optional additional service configuration</param>
    /// <returns>The builder for method chaining</returns>
    public static DataFlowGraphBuilder AddEpochBuffer<T>(
        this DataFlowGraphBuilder builder,
        string name,
        Func<IServiceProvider, IBufferConfiguration> bufferConfiguration,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(bufferConfiguration);

        return builder.AddBlock<EpochBufferBlock<T>>(
            name,
            services =>
            {
                // Register custom buffer configuration factory
                services.AddTransient(bufferConfiguration);

                // Allow caller to register additional services
                configureServices?.Invoke(services);
            });
    }
}
