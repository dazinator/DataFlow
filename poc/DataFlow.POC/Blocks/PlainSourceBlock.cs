namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Source block that hosts a plain source actor producing continuous data streams
/// without epoch knowledge. For epoch-based processing, connect this block's output
/// to an EpochSegmenterBlock.
/// </summary>
/// <typeparam name="T">The type of items produced</typeparam>
/// <typeparam name="TActor">The plain source actor type</typeparam>
public sealed class PlainSourceBlock<T, TActor> : BlockBase<object, T>
    where TActor : IPlainSourceActor<T>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActorExecutionContext _context = new();

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
    /// All dependencies are passed via constructor.
    /// </summary>
    public PlainSourceBlock(IBlockContext context, IServiceScopeFactory scopeFactory)
        : base(context)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public override async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        // Source blocks ignore input - they generate data
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        InitializeActorContext(context);
        var actor = scope.ServiceProvider.GetRequiredService<TActor>();
        
        // Stream plain items from the actor
        await foreach (var item in actor.ProduceAsync(_context).WithCancellation(context.CancellationToken))
        {
            yield return item;
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
