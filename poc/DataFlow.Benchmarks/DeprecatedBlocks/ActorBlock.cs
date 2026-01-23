namespace DataFlow.POC.Benchmarks.DeprecatedBlocks;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using DataFlow.POC.Registry;

/// <summary>
/// ⚠️ DEPRECATED - Benchmark-only. Use EpochActorBlock for new code.
/// 
/// Plain (non-epoch) actor block for backward compatibility with benchmarks.
/// This block is retained solely for benchmark comparisons between plain and epoch-based processing.
/// New code should use EpochActorBlock instead.
/// </summary>
/// <typeparam name="TIn">Input item type</typeparam>
/// <typeparam name="TOut">Output item type</typeparam>
/// <typeparam name="TActor">Actor type implementing IStreamActor</typeparam>
/// <remarks>
/// This block was part of the pre-epoch architecture and has been deprecated in favor of
/// the unified epoch-based architecture. It is maintained only for benchmark compatibility.
/// </remarks>
[Obsolete("ActorBlock is deprecated. Use EpochActorBlock with PlainSourceAdapter for new code.")]
public sealed class ActorBlock<TIn, TOut, TActor> : BlockBase<TIn, TOut>
    where TActor : IStreamActor<TIn, TOut>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ActorExecutionContext _context = new();

    /// <summary>
    /// Constructor for backward compatibility with benchmarks.
    /// </summary>
    /// <param name="name">Block name</param>
    /// <param name="scopeFactory">Service scope factory for DI</param>
    public ActorBlock(string name, IServiceScopeFactory scopeFactory)
        : base(new BlockContext(name))
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <summary>
    /// Constructor with IBlockContext for proper dependency injection.
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
        await using var scope = _scopeFactory.CreateAsyncScope();
        
        InitializeActorContext(context);
        var actor = scope.ServiceProvider.GetRequiredService<TActor>();
        
        var output = actor.RunAsync(input, _context);
        
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
            () => { }); // Plain blocks don't support rotation
    }
}
