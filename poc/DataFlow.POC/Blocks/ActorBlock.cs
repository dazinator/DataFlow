namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

/// <summary>
/// Actor block that hosts a scoped actor with DI scope rotation capability.
/// Each actor runs in its own async DI scope and can request rotation when appropriate.
/// </summary>
/// <typeparam name="TIn">Input item type</typeparam>
/// <typeparam name="TOut">Output item type</typeparam>
/// <typeparam name="TActor">Actor type implementing IStreamActor</typeparam>
public sealed class ActorBlock<TIn, TOut, TActor> : BlockBase<TIn, TOut>
    where TActor : IStreamActor<TIn, TOut>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActorExecutionContext _context = new();

    public ActorBlock(string name, IServiceScopeFactory scopeFactory)
        : base(name)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public override async IAsyncEnumerable<TOut> ExecuteAsync(
        IAsyncEnumerable<TIn> input,
        IExecutionContext context)
    {
        await using var inputEnumerator = input.GetAsyncEnumerator(context.CancellationToken);

        while (!context.CancellationToken.IsCancellationRequested)
        {
            bool rotationRequested = false;

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                InitializeActorContext(context, () => rotationRequested = true);
                var actor = scope.ServiceProvider.GetRequiredService<TActor>();
                
                var actorInput = CreateActorInputStream(inputEnumerator, context.CancellationToken);
                var actorOutput = actor.RunAsync(actorInput, _context);

                // Yield items from actor as they come
                await foreach (var item in actorOutput.WithCancellation(context.CancellationToken))
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
        Action onRotationRequested)
    {
        _context.Reset(
            context.CancellationToken,
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
