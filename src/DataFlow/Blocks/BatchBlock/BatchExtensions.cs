#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Uniun.DataFlow.Blocks;
#pragma warning restore IDE0130 // Namespace does not match folder structure
using System;
using Uniun.DataFlow.Blocks.BatchBlock;
using Uniun.DataFlow.Builder;

public static class BatchExtensions
{
    /// <summary>
    /// Adds a batch block that collects items into arrays based on size and time window criteria.
    /// </summary>
    /// <typeparam name="T">The type of items to batch</typeparam>
    /// <param name="builder">The data flow builder</param>
    /// <param name="name">Name of the block</param>
    /// <param name="maxBatchSize">Maximum number of items in a batch</param>
    /// <param name="windowPeriod">Time window after which a batch will be emitted even if not full</param>
    /// <param name="options">Optional block configuration options</param>
    /// <returns>A builder for configuring the batch block</returns>
    public static IPropagatingBlockBuilder<T, T[]> AddBatch<T>(
        this IDataFlowBuilder builder,
        string name,
        int maxBatchSize,
        TimeSpan windowPeriod,
        BlockOptions? options = null)
    {
        var block = new BatchBlock<T>(maxBatchSize, windowPeriod, options);
        return builder.AddPropagatorBlock(name, block);
    }
}
