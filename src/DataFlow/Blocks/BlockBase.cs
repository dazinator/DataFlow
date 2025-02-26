namespace Uniun.DataFlow.Blocks;
using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow;

/* Unmerged change from project 'DataFlow (net6.0)'
Added:
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Blocks;
using Uniun.DataFlow.Blocks.Core;
*/

public abstract class BlockBase : IBlock
{
    // private readonly List<IMiddleware> _middleware = new();

    protected BlockBase(string name, BlockOptions? blockOptions)
    {
        Options = blockOptions ?? new BlockOptions();
        Name = name;
    }

    public BlockOptions Options { get; }
    public string Name { get; }

    //public void AddMiddleware(IMiddleware middleware)
    //{
    //    _middleware.Add(middleware);
    //}

    public async Task ExecuteAsync(IDataFlowContext context)
    {
        // Build middleware pipeline

        // var branchContext = context.CreateBranch(Guid.NewGuid(), GetType().Name);

        //PipelineStepDelegate pipeline = CoreExecuteAsync;

        //// Add middleware in reverse order
        //foreach (var middleware in _middleware.AsEnumerable().Reverse())
        //{
        //    var next = pipeline;
        //    pipeline = ctx => middleware.ExecuteAsync(next, ctx);
        //}

        // We branch the pipeline for each block, because pipeline context is not designed for concurrent access, and blocks execute concurrently.
        //await pipeline(branchContext);

        await CoreExecuteAsync(context);
    }

    protected abstract Task CoreExecuteAsync(IDataFlowContext context);


    protected virtual async Task ExecuteParallelActivities(IDataFlowContext context, int howMany, ParallelActivityDelegate activity)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Options.MaxConcurrency,
            CancellationToken = context.CancellationToken
        };

        await Parallel.ForEachAsync(
       Enumerable.Range(0, howMany),
       parallelOptions,
       async (index, ct) =>
       {
           if (!Options.UseSeperateScopes)
           {
               await activity(index, context);
               return;
           }
           await using var scope = context.ServiceProvider.CreateAsyncScope();
           var branchContext = new DataFlowContext()
           {
               CancellationToken = context.CancellationToken,
               ServiceProvider = scope.ServiceProvider
           };
           await activity(index, branchContext);
       });
    }

    // protected virtual Task StartConcurrentTaskAsync(int index, IDataFlowContext branchContext) => Task.CompletedTask; // no op by default.

    // protected abstract Task CoreExecuteAsync(PipelineContext context);
}

public delegate Task ParallelActivityDelegate(int index, IDataFlowContext context);


