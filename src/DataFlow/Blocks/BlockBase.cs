namespace Uniun.DataFlow.Blocks;

using System;
using Microsoft.Extensions.Logging;
using Uniun.DataFlow;


public abstract class BlockBase : IBlock
{
    protected BlockBase(string name, BlockOptions? blockOptions, ILogger logger)
    {
        Options = blockOptions ?? new BlockOptions();
        Logger = logger;
        Name = name;       
    }

    public BlockOptions Options { get; }
    public string Name { get; }

    public BlockMetricsTagsContext MetricsContext { get; set; }

    public ILogger Logger { get; }
    //public DataItemMetricsContext ItemsMetricContext { get; set; }

    public async Task ExecuteAsync(IDataFlowContext context)
    {
        using var logScope = Logger.BeginScope(new Dictionary<string, object> { { "BlockName", Name } });
        Logger.LogInformation("Executing");
        //ItemsMetricContext = MetricsContext.CreateItemsContext(Options.ItemsMetricLabel);
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
                            Logger.LogDebug(
                                "Parallel activity starting with service provider {ServiceProvider}",
                                context.ServiceProvider.GetHashCode());

                            await activity(index, context);
                            return;
                        }

                        try
                        {
                            // Wrap scope creation in try-catch to handle disposed provider
                            await using var scope = context.CreateNewAsyncScope(out var branchContext);

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
            throw;
        }
    }
}

public delegate Task ParallelActivityDelegate(int index, IDataFlowContext context);


