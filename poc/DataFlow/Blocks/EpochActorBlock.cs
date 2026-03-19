namespace DataFlow.POC.Blocks;

using System;
using System.Runtime.CompilerServices;
using DataFlow.Blazor.Events;
using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// All dependencies are passed via constructor.
    /// </summary>
    public EpochActorBlock(IBlockContext context, IServiceScopeFactory scopeFactory)
        : base(context)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public override async IAsyncEnumerable<IEpochStream<TOut>> ExecuteAsync(
        IAsyncEnumerable<IEpochStream<TIn>> input,
        IExecutionContext context)
    {
        await foreach (var epochStream in input.WithCancellation(context.CancellationToken))
        {
            // Create output epoch stream with same epoch metadata
            yield return new EpochStream<TOut>(
                epochStream.Epoch,
                ProcessEpochItems(epochStream, context, context.CancellationToken));
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


        // Note: This logic maintains an input stream enumerator - and allows an actor instance to process items from it.
        //       If: the actor returns early signaling rotation, then we iterate in the while loop creating a new actor instance
        //       but continuing from the same enumerator. This means that position in the input stream is preserved across actor instance rotations.
        //       actor rotation allows a new DI scope to be created for further procesing, which is useful for refreshing scoped dependencies - allowing memory to be collected
        //       or resetting stateful services that the actor might be accruing over time whilst processing from the stream (not recommended but sometimes unavoidable).
        //       If: the actor finishes without requesting rotation, then we exit the while loop and complete processing of this epoch stream.
        while (!cancellationToken.IsCancellationRequested)
        {
            bool rotationRequested = false;

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                InitializeActorContext(context, scope.ServiceProvider, () => rotationRequested = true, cancellationToken);
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
            {
                yield break;
            }
        }
    }

    private void InitializeActorContext(
        IExecutionContext context,
        IServiceProvider actorScopeProvider,
        Action onRotationRequested,
        CancellationToken cancellationToken)
    {
        var sink = actorScopeProvider.GetService(typeof(IFlowEventSink)) as IFlowEventSink;
        var emitter = sink is not null ? new BoundFlowEventEmitter(sink, context.InvocationId) : null;

        _context.Reset(
            cancellationToken,
            context.InvocationId,
            onRotationRequested,
            epochCoordinator: null,
            triggerContext: context.TriggerContext,
            events: emitter);
    }






    /// <summary>
    /// CreateActorInputStream IS NECESSARY for correctness.    ///
    /// Reason: The DI scope rotation feature requires multiple actor instances to share progress through the input stream.The wrapper:
    ///         Maintains a single shared enumerator across actor instances
    ///         Allows each new actor to continue from where the previous one left off
    ///         Prevents infinite loops or duplicate processing
    ///         Without the wrapper: Each actor would call GetAsyncEnumerator() on epochStream.Items, creating a fresh enumerator that starts from the beginning.
    /// </summary>
    /// <param name="enumerator"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
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
