namespace Uniun.DataFlow.Blocks.RateLimiting;
using System;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow.Metrics;

/// <summary>
/// Extension methods for adding rate limiting blocks
/// </summary>
public static class RateLimitExtensions
{
    /// <summary>
    /// Adds a rate limiting block with a factory function to create the rate limiter
    /// </summary>
    public static IPropagatingBlockBuilder<T, T> AddRateLimit<T>(
        this IDataFlowBuilder builder,
        string name,
        Func<RateLimiter> rateLimiterFactory,
        Action<BlockOptions>? configureOptions = null)
    {
        var blockOptions = new BlockOptions
        {
            Capacity = 1
        };
        configureOptions?.Invoke(blockOptions);

        var block = new RateLimitBlock<T>(
            name,
            builder.ServiceProvider.GetRequiredService<ILogger<RateLimitBlock<T>>>(),
            builder.ServiceProvider.GetRequiredService<IBoundedChannelFactory>(),
            blockOptions,
            rateLimiterFactory,
            builder.ServiceProvider.GetService<IDataFlowMetrics>());

        return builder.AddPropagatorBlock(name, block);
    }
}
