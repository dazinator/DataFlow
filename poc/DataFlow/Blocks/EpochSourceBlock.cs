namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Source block that hosts a source actor producing epoch streams.
/// This block manages the lifecycle and DI scope of the source actor,
/// allowing it to produce data with epoch boundaries without restarting.
/// The coordinator is passed through execution context to actors.
/// </summary>
/// <typeparam name="T">The type of items produced</typeparam>
/// <typeparam name="TActor">The source actor type</typeparam>
public sealed class EpochSourceBlock<T, TActor> : BlockBase<object, IEpochStream<T>>
    where TActor : ISourceActor<T>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActorExecutionContext _context = new();
    private readonly IEpochCoordinator _coordinator;

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// All dependencies are passed via constructor.
    /// </summary>
    public EpochSourceBlock(IBlockContext context, IServiceScopeFactory scopeFactory, IEpochCoordinator coordinator)
        : base(context)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        // Source blocks ignore input - they generate data
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        // Initialize context WITH coordinator
        InitializeActorContext(context, _coordinator);
        
        // Resolve actor normally from application DI
        var actor = scope.ServiceProvider.GetRequiredService<TActor>();
        
        // Stream epoch streams from the actor
        // Actors get coordinator from context
        await foreach (var epochStream in actor.ProduceEpochsAsync(_context).WithCancellation(context.CancellationToken))
        {
            yield return epochStream;
        }
    }

    private void InitializeActorContext(IExecutionContext context, IEpochCoordinator coordinator)
    {
        _context.Reset(
            context.CancellationToken,
            context.InvocationId,
            () => { }, // Source actors don't rotate
            coordinator);
    }
}
