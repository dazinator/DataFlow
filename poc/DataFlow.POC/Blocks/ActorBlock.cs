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
/// <remarks>
/// <para>
/// <strong>⚠️ DEPRECATED:</strong> This plain variant of ActorBlock is deprecated in favor of the unified epoch-based architecture.
/// All blocks now use epochs by default. Plain sources are automatically wrapped in single-epoch streams.
/// </para>
/// <para>
/// <strong>Migration Guide:</strong>
/// </para>
/// <list type="bullet">
/// <item>If your source produces plain items: Use <c>PlainSourceAdapter&lt;T, TActor&gt;</c> or call <c>.WrapInSingleEpoch(sourceName)</c> on your stream.</item>
/// <item>If your source is already epoch-aware: Use <c>EpochActorBlock&lt;TIn, TOut, TActor&gt;</c> directly (no changes needed).</item>
/// <item>For automatic wrapping in graph builder: Use <c>builder.AddPlainSource&lt;T, TActor&gt;(name)</c>.</item>
/// </list>
/// <para>
/// <strong>Performance:</strong> Single-epoch wrapping has &lt;5% overhead (research validated: 4.08%).
/// </para>
/// <para>
/// <strong>Removal Timeline:</strong> This class will be removed in v3.0 (Q1 2027).
/// </para>
/// </remarks>
[Obsolete("Use EpochActorBlock for epoch-aware processing. For plain sources, use PlainSourceAdapter or WrapInSingleEpoch extension method. See XML docs for migration guide.", false)]
public sealed class ActorBlock<TIn, TOut, TActor> : BlockBase<TIn, TOut>
    where TActor : IStreamActor<TIn, TOut>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActorExecutionContext _context = new();

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// All dependencies are passed via constructor.
    /// </summary>
    public ActorBlock(IBlockContext context, IServiceScopeFactory scopeFactory)
        : base(context)
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
