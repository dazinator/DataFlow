namespace DataFlow.POC.Builder;

using DataFlow.POC.Blocks;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding epoch buffer blocks to the data flow graph.
/// </summary>
public static class EpochBufferBlockExtensions
{
    /// <summary>
    /// Adds an epoch-aware buffer block to the graph.
    /// The buffer preserves epoch boundaries while buffering items within each epoch.
    /// </summary>
    /// <typeparam name="T">The type of items to buffer.</typeparam>
    /// <param name="builder">The data flow graph builder.</param>
    /// <param name="name">Unique name for the buffer block.</param>
    /// <param name="capacity">Maximum number of items to buffer per epoch (must be greater than 0).</param>
    /// <param name="configureServices">Optional action to configure additional services for the block.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when capacity is less than or equal to 0.</exception>
    /// <remarks>
    /// <para>
    /// Use this method to add buffering between blocks in an epoch stream pipeline.
    /// This is particularly useful when:
    /// <list type="bullet">
    /// <item><description>Multiple producers need to send to a single consumer (fan-in)</description></item>
    /// <item><description>You need to smooth out rate variations between producers and consumers</description></item>
    /// <item><description>You want to decouple producer and consumer processing rates</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// The buffer capacity is per-epoch. When an epoch's buffer fills up, the producer
    /// will block until space becomes available, providing natural backpressure.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var builder = new DataFlowGraphBuilder("my-graph", serviceProvider, registry);
    /// builder.AddEpochBuffer&lt;int&gt;("buffer", capacity: 100);
    /// </code>
    /// </example>
    public static DataFlowGraphBuilder AddEpochBuffer<T>(
        this DataFlowGraphBuilder builder,
        string name,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        
        if (capacity <= 0)
        {
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));
        }

        var bufferConfig = new BufferConfiguration(capacity);
        var context = new Core.BlockContext(name);
        var block = new EpochBufferBlock<T>(context, bufferConfig);
        
        return builder.AddBlock(block);
    }

    /// <summary>
    /// Adds an epoch-aware buffer block with a custom configuration.
    /// </summary>
    /// <typeparam name="T">The type of items to buffer.</typeparam>
    /// <param name="builder">The data flow graph builder.</param>
    /// <param name="name">Unique name for the buffer block.</param>
    /// <param name="bufferConfiguration">The buffer configuration to use.</param>
    /// <returns>The builder for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    /// <remarks>
    /// Use this overload when you need to provide a custom buffer configuration instance.
    /// </remarks>
    /// <example>
    /// <code>
    /// var config = new BufferConfiguration(capacity: 100);
    /// builder.AddEpochBuffer&lt;int&gt;("buffer", config);
    /// </code>
    /// </example>
    public static DataFlowGraphBuilder AddEpochBuffer<T>(
        this DataFlowGraphBuilder builder,
        string name,
        IBufferConfiguration bufferConfiguration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(bufferConfiguration);

        var context = new Core.BlockContext(name);
        var block = new EpochBufferBlock<T>(context, bufferConfiguration);
        
        return builder.AddBlock(block);
    }
}
