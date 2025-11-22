namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DEPRECATED: Plain source block for backward compatibility with benchmarks.
/// This block is retained solely for benchmark comparisons between plain and epoch-based processing.
/// New code should use PlainSourceAdapter instead.
/// </summary>
/// <typeparam name="T">The type of items produced</typeparam>
/// <typeparam name="TActor">The plain source actor type</typeparam>
/// <remarks>
/// This block was part of the pre-epoch architecture and has been deprecated in favor of
/// PlainSourceAdapter which automatically wraps plain sources in epochs.
/// It is maintained only for benchmark compatibility.
/// </remarks>
[Obsolete("PlainSourceBlock is deprecated. Use PlainSourceAdapter for new code.")]
public sealed class PlainSourceBlock<T, TActor> : BlockBase<object, T>
    where TActor : IPlainSourceActor<T>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActorExecutionContext _context = new();

    /// <summary>
    /// Constructor for backward compatibility with benchmarks.
    /// </summary>
    /// <param name="name">Block name</param>
    /// <param name="scopeFactory">Service scope factory for DI</param>
    public PlainSourceBlock(string name, IServiceScopeFactory scopeFactory)
        : base(new BlockContext(name))
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
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
        
        var output = actor.ProduceAsync(_context);
        
        await foreach (var item in output.WithCancellation(context.CancellationToken))
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
