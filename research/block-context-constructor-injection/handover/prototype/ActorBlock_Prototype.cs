// Prototype: ActorBlock with Constructor Injection
// This demonstrates how ActorBlock constructor changes with the new pattern

namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

/// <summary>
/// PROTOTYPE: ActorBlock with IBlockContext in constructor.
/// All dependencies are passed via constructor - no post-construction setup needed.
/// </summary>
public sealed class ActorBlock_Prototype<TIn, TOut, TActor> : BlockBase_Prototype<TIn, TOut>
    where TActor : IStreamActor<TIn, TOut>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActorExecutionContext _context = new();

    /// <summary>
    /// Constructor with all dependencies including IBlockContext.
    /// This is the only constructor - no parameterless version.
    /// </summary>
    /// <param name="blockContext">Block context with name and metadata</param>
    /// <param name="scopeFactory">Service scope factory for actor creation</param>
    public ActorBlock_Prototype(IBlockContext blockContext, IServiceScopeFactory scopeFactory)
        : base(blockContext)
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

                await foreach (var item in actorOutput.WithCancellation(context.CancellationToken))
                {
                    yield return item;
                }
            }

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
