namespace DataFlow.POC.Benchmarks;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using DataFlow.POC.Blocks;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Shared utilities for benchmark actor creation and service provider setup.
/// Reduces duplication across benchmark files.
/// </summary>
public static class BenchmarkActorHelpers
{
    /// <summary>
    /// Generic no-op processor actor for benchmarks.
    /// Consumes items and optionally invokes a callback.
    /// </summary>
    public class NoOpProcessorActor<T> : IStreamActor<T, object>
    {
        private readonly Action? _onProcessed;

        public NoOpProcessorActor(Action? onProcessed = null)
        {
            _onProcessed = onProcessed;
        }

        public async IAsyncEnumerable<object> RunAsync(
            IAsyncEnumerable<T> input,
            [EnumeratorCancellation] IActorExecutionContext context)
        {
            await foreach (var item in input.WithCancellation(context.CancellationToken))
            {
                _onProcessed?.Invoke();
            }
            yield break;
        }
    }

    /// <summary>
    /// Creates an ActorBlock with simplified service provider setup.
    /// Reduces the boilerplate of creating ServiceCollection, adding scoped service, and building provider.
    /// </summary>
    /// <typeparam name="TIn">Input type for the actor</typeparam>
    /// <typeparam name="TOut">Output type for the actor</typeparam>
    /// <typeparam name="TActor">Actor implementation type</typeparam>
    /// <param name="name">Block name</param>
    /// <param name="actorFactory">Factory function to create the actor instance</param>
    /// <returns">Configured ActorBlock instance</returns>
    [Obsolete("ActorBlock is deprecated. Use EpochActorBlock for new code.")]
    public static ActorBlock<TIn, TOut, TActor> CreateActorBlock<TIn, TOut, TActor>(
        string name,
        Func<TActor> actorFactory)
        where TActor : class, IStreamActor<TIn, TOut>
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => actorFactory());
        var serviceProvider = services.BuildServiceProvider();
        
        return new ActorBlock<TIn, TOut, TActor>(
            name,
            serviceProvider.GetRequiredService<IServiceScopeFactory>());
    }

    /// <summary>
    /// Creates multiple ActorBlocks with the same actor type but different instances.
    /// Useful for creating competing consumers in benchmarks.
    /// </summary>
    /// <typeparam name="TIn">Input type for the actor</typeparam>
    /// <typeparam name="TOut">Output type for the actor</typeparam>
    /// <typeparam name="TActor">Actor implementation type</typeparam>
    /// <param name="namePrefix">Prefix for block names (will append index)</param>
    /// <param name="count">Number of blocks to create</param>
    /// <param name="actorFactory">Factory function to create actor instances</param>
    /// <returns>List of configured ActorBlock instances</returns>
    [Obsolete("ActorBlock is deprecated. Use EpochActorBlock for new code.")]
    public static List<ActorBlock<TIn, TOut, TActor>> CreateActorBlocks<TIn, TOut, TActor>(
        string namePrefix,
        int count,
        Func<int, TActor> actorFactory)
        where TActor : class, IStreamActor<TIn, TOut>
    {
        var blocks = new List<ActorBlock<TIn, TOut, TActor>>();
        
        for (int i = 0; i < count; i++)
        {
            var index = i;
            var block = CreateActorBlock<TIn, TOut, TActor>(
                $"{namePrefix}-{i}",
                () => actorFactory(index));
            blocks.Add(block);
        }
        
        return blocks;
    }
}
