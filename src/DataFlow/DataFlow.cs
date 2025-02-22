namespace Uniun.DataFlow;

using Uniun.DataFlow.Blocks;

// Generic wrapper that uses the config type as the type parameter
public class DataFlow<TConfig> : IDataFlow
    where TConfig : IDataFlowConfiguration
{
    private readonly DataFlow _flow;

    public DataFlow(DataFlow flow)
    {
        _flow = flow;
    }

    public Task ExecuteAsync(IDataFlowContext context) => _flow.ExecuteAsync(context);
}

public class DataFlow
{
    private readonly List<IBlock> _blocks;

    public DataFlow(List<IBlock> blocks)
    {
        _blocks = blocks;
    }

    public async Task ExecuteAsync(IDataFlowContext context)
    {
        // Execute all blocks concurrently
        var blockTasks = _blocks.Select(block =>
            ExecuteBlockAsync(block, context));

        await Task.WhenAll(blockTasks);
        context.CancellationToken.ThrowIfCancellationRequested(); // becuse channel readers writers can gracefully exit from streams, lets ensure if we are cancelled we throw here.
    }

    private async Task ExecuteBlockAsync(IBlock block, IDataFlowContext context)
    {
        await block.ExecuteAsync(context);
    }
}
