namespace Uniun.DataFlow.Blocks;

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    protected BlockBase(string name, BlockOptions? blockOptions, ILogger logger)
    {
        Options = blockOptions ?? new BlockOptions();
        Logger = logger;
        Name = name;
    }

    public BlockOptions Options { get; }
    public string Name { get; }

    public ILogger Logger { get; }

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

        using var logScope = Logger.BeginScope(new Dictionary<string, object> { { "BlockName", Name } });
        Logger.LogInformation("Executing");
        await CoreExecuteAsync(context);
        Logger.LogInformation("Finished Executing");
    }

    protected abstract Task CoreExecuteAsync(IDataFlowContext context);


    // 2. Add simple exception handling in BlockBase.ExecuteParallelActivities
    protected virtual async Task ExecuteParallelActivities(IDataFlowContext context, int howMany, ParallelActivityDelegate activity)
    {
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Options.MaxConcurrency,
            CancellationToken = context.CancellationToken
        };

        try
        {
            await Parallel.ForEachAsync(
                Enumerable.Range(0, howMany),
                parallelOptions,
                async (index, ct) =>
                {

                    try
                    {
                        using var logScope = Logger.BeginScope(new Dictionary<string, object> { { "ParallelIndex", index } });

                        if (!Options.UseSeperateScopes)
                        {

                            //string actorId = Guid.NewGuid().ToString().Substring(0, 8); // Generate unique ID for this actor
                            Logger.LogDebug(
                                "Parallel activity starting with service provider {ServiceProvider}",
                                context.ServiceProvider.GetHashCode());

                            await activity(index, context);
                            return;
                        }

                        try
                        {
                            // Wrap scope creation in try-catch to handle disposed provider
                            await using var scope = context.ServiceProvider.CreateAsyncScope();

                            var branchContext = new DataFlowContext()
                            {
                                CancellationToken = context.CancellationToken,
                                ServiceProvider = scope.ServiceProvider
                            };

                            //string actorId = Guid.NewGuid().ToString().Substring(0, 8); // Generate unique ID for this actor
                            Logger.LogDebug(
                                "Parallel activity starting with service provider {ServiceProvider}",
                                branchContext.ServiceProvider.GetHashCode());

                            await activity(index, branchContext);
                        }
                        catch (ObjectDisposedException ex)
                        {
                            // Just log and exit gracefully if provider was disposed
                            Logger.LogWarning(
                                ex,
                                "Service provider was disposed while creating scope");
                        }
                    }
                    finally
                    {
                        Logger.LogDebug(
                             "Parallel activity completed with service provider {ServiceProvider}",
                             context.ServiceProvider.GetHashCode());
                    }
                });
        }
        catch (ObjectDisposedException ex)
        {
            // Catch provider disposal at the outer level as well
            Logger.LogWarning(
                ex,
                "Service provider was disposed during parallel activities in block {blockName}",
                Name);
        }
    }

    // protected virtual Task StartConcurrentTaskAsync(int index, IDataFlowContext branchContext) => Task.CompletedTask; // no op by default.

    // protected abstract Task CoreExecuteAsync(PipelineContext context);
}

public delegate Task ParallelActivityDelegate(int index, IDataFlowContext context);


