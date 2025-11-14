namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Source block that hosts a source actor producing epoch streams.
/// This block manages the lifecycle and DI scope of the source actor,
/// allowing it to produce data with epoch boundaries without restarting.
/// All source actors now use IEpochCoordinator internally for epoch management.
/// </summary>
/// <typeparam name="T">The type of items produced</typeparam>
/// <typeparam name="TActor">The source actor type</typeparam>
public sealed class EpochSourceBlock<T, TActor> : BlockBase<object, IEpochStream<T>>
    where TActor : ISourceActor<T>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActorExecutionContext _context = new();

    public EpochSourceBlock(string name, IServiceScopeFactory scopeFactory)
        : base(name)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public override async IAsyncEnumerable<IEpochStream<T>> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        // Source blocks ignore input - they generate data
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        InitializeActorContext(context);
        var actor = scope.ServiceProvider.GetRequiredService<TActor>();
        
        // Stream epoch streams from the actor
        // Actors now handle coordination internally via IEpochCoordinator
        await foreach (var epochStream in actor.ProduceEpochsAsync(_context).WithCancellation(context.CancellationToken))
        {
            yield return epochStream;
        }
    }

    private void InitializeActorContext(IExecutionContext context)
    {
        _context.Reset(
            context.CancellationToken,
            context.InvocationId,
            () => { }); // Source actors don't rotate
    }
}
