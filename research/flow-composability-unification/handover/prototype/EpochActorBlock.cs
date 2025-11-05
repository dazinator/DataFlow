namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using DataFlow.POC.Epoch;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

/// <summary>
/// Epoch-aware actor block that hosts a scoped actor with DI scope rotation capability.
/// Each actor runs in its own async DI scope and can request rotation when appropriate.
/// Processes items within epoch boundaries while preserving epoch structure.
/// </summary>
/// <typeparam name="TIn">Input item type</typeparam>
/// <typeparam name="TOut">Output item type</typeparam>
/// <typeparam name="TActor">Actor type implementing IStreamActor</typeparam>
/// <remarks>
/// This is the RECOMMENDED implementation for epoch-aware stream processing.
/// It consolidates transformation and processing capabilities with DI safety.
/// 
/// Key Design Points:
/// - Mirrors ActorBlock pattern for consistency
/// - Preserves epoch boundaries (no cross-epoch processing)
/// - DI scope isolation prevents concurrent dependency sharing bugs
/// - Optional scope rotation for stateful dependencies
/// - Epoch boundaries can serve as natural rotation points
/// </remarks>
public sealed class EpochActorBlock<TIn, TOut, TActor> : BlockBase<IEpochStream<TIn>, IEpochStream<TOut>>
    where TActor : IStreamActor<TIn, TOut>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActorExecutionContext _context = new();

    public EpochActorBlock(string name, IServiceScopeFactory scopeFactory)
        : base(name)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public override async IAsyncEnumerable<IEpochStream<TOut>> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<TIn>> input,
        IExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var epochStream in input.WithCancellation(cancellationToken))
        {
            // Create output epoch stream with same epoch metadata
            yield return new EpochStream<TOut>(
                epochStream.Epoch,
                ProcessEpochItems(epochStream, context, cancellationToken));
        }
    }

    private async IAsyncEnumerable<TOut> ProcessEpochItems(
        IEpochStream<TIn> epochStream,
        IExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Process items within this epoch stream using actor pattern
        // Actor runs in its own DI scope with rotation capability
        await using var inputEnumerator = epochStream.Items.GetAsyncEnumerator(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            bool rotationRequested = false;

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                InitializeActorContext(context, () => rotationRequested = true, cancellationToken);
                var actor = scope.ServiceProvider.GetRequiredService<TActor>();

                var actorInput = CreateActorInputStream(inputEnumerator, cancellationToken);
                var actorOutput = actor.RunAsync(actorInput, _context);

                // Yield items from actor as they come
                await foreach (var item in actorOutput.WithCancellation(cancellationToken))
                {
                    yield return item;
                }
            }

            // After actor completes, check if rotation was requested
            if (!rotationRequested)
                yield break;
        }
    }

    private void InitializeActorContext(
        IExecutionContext context,
        Action onRotationRequested,
        CancellationToken cancellationToken)
    {
        _context.Reset(
            cancellationToken,
            context.InvocationId,
            onRotationRequested);
    }

    private static async IAsyncEnumerable<TIn> CreateActorInputStream(
        IAsyncEnumerator<TIn> enumerator,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await enumerator.MoveNextAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return enumerator.Current;
        }
    }
}

/// <summary>
/// Helper factory methods for common EpochActorBlock patterns
/// </summary>
public static class EpochActorBlockExtensions
{
    /// <summary>
    /// Creates an EpochActorBlock for simple 1-to-1 transformations
    /// </summary>
    public static EpochActorBlock<TIn, TOut, SimpleTransformerActor<TIn, TOut>> CreateEpochTransformer<TIn, TOut>(
        string name,
        IServiceScopeFactory scopeFactory,
        Func<TIn, TOut> transform)
    {
        // Register SimpleTransformerActor<TIn, TOut> in DI with the transform function
        return new EpochActorBlock<TIn, TOut, SimpleTransformerActor<TIn, TOut>>(name, scopeFactory);
    }

    /// <summary>
    /// Creates an EpochActorBlock for processing with side effects (terminal block)
    /// </summary>
    public static EpochActorBlock<T, object, SimpleProcessorActor<T>> CreateEpochProcessor<T>(
        string name,
        IServiceScopeFactory scopeFactory,
        Func<T, Task> process)
    {
        // Register SimpleProcessorActor<T> in DI with the process function
        return new EpochActorBlock<T, object, SimpleProcessorActor<T>>(name, scopeFactory);
    }
}
