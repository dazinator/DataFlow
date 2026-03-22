namespace DataFlow.Blazor.Demo.Server.Flows;

using DataFlow.POC.Core;

/// <summary>
/// Produces a stream of integer items with a realistic delay between each.
/// </summary>
public sealed class DemoProducerBlock : BlockBase<object, int>
{
    private readonly int _itemCount;
    private readonly int _delayMs;

    public DemoProducerBlock(string name, int itemCount = 50, int delayMs = 80)
        : base(new BlockContext(name))
    {
        _itemCount = itemCount;
        _delayMs = delayMs;
    }

    public override async IAsyncEnumerable<int> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        for (int i = 1; i <= _itemCount; i++)
        {
            await Task.Delay(_delayMs, context.CancellationToken);
            yield return i;
        }
    }
}

/// <summary>
/// Transforms each integer by multiplying by 2, simulating CPU work.
/// </summary>
public sealed class DemoTransformBlock : BlockBase<int, int>
{
    public DemoTransformBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<int> ExecuteAsync(
        IAsyncEnumerable<int> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            await Task.Delay(10, context.CancellationToken); // simulate work
            yield return item * 2;
        }
    }
}

/// <summary>
/// Collects items into batches of a fixed size and emits each batch as a list.
/// </summary>
public sealed class DemoBatchBlock : BlockBase<int, List<int>>
{
    private readonly int _batchSize;

    public DemoBatchBlock(string name, int batchSize = 5) : base(new BlockContext(name))
    {
        _batchSize = batchSize;
    }

    public override async IAsyncEnumerable<List<int>> ExecuteAsync(
        IAsyncEnumerable<int> input,
        IExecutionContext context)
    {
        var batch = new List<int>(_batchSize);
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            batch.Add(item);
            if (batch.Count >= _batchSize)
            {
                yield return batch;
                batch = new List<int>(_batchSize);
            }
        }
        if (batch.Count > 0) yield return batch;
    }
}

/// <summary>
/// Terminal sink — processes each batch with a small delay. Yields nothing.
/// </summary>
public sealed class DemoProcessorBlock : BlockBase<List<int>, object>
{
    private readonly int _delayMs;

    public DemoProcessorBlock(string name, int delayMs = 30) : base(new BlockContext(name))
    {
        _delayMs = delayMs;
    }

    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<List<int>> input,
        IExecutionContext context)
    {
        await foreach (var batch in input.WithCancellation(context.CancellationToken))
        {
            await Task.Delay(_delayMs, context.CancellationToken);
            // terminal — nothing to yield
        }
        yield break;
    }
}

/// <summary>
/// Routes integer items: odd → high priority output, even → low priority output.
/// Each item is tagged with its priority so downstream can identify the route.
/// </summary>
public sealed class DemoRouterBlock : BlockBase<int, (int Value, string Priority)>
{
    public DemoRouterBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<(int Value, string Priority)> ExecuteAsync(
        IAsyncEnumerable<int> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
        {
            await Task.Delay(5, context.CancellationToken);
            yield return (item, item % 2 != 0 ? "high" : "low");
        }
    }
}

/// <summary>
/// Accepts tagged items and processes only those matching its priority lane.
/// </summary>
public sealed class DemoPriorityProcessorBlock : BlockBase<(int Value, string Priority), object>
{
    private readonly string _priority;
    private readonly int _delayMs;

    public DemoPriorityProcessorBlock(string name, string priority, int delayMs = 20)
        : base(new BlockContext(name))
    {
        _priority = priority;
        _delayMs = delayMs;
    }

    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<(int Value, string Priority)> input,
        IExecutionContext context)
    {
        await foreach (var (value, priority) in input.WithCancellation(context.CancellationToken))
        {
            if (priority == _priority)
                await Task.Delay(_delayMs, context.CancellationToken);
        }
        yield break;
    }
}

/// <summary>
/// Slow terminal sink — processes each item individually with a large delay.
/// Used in the backpressure demo to ensure the upstream buffer stays full.
/// </summary>
public sealed class DemoSlowConsumerBlock : BlockBase<int, object>
{
    private readonly int _delayMs;

    public DemoSlowConsumerBlock(string name, int delayMs = 300) : base(new BlockContext(name))
    {
        _delayMs = delayMs;
    }

    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<int> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
            await Task.Delay(_delayMs, context.CancellationToken);
        yield break;
    }
}

/// <summary>
/// Merges integer items from multiple sources (fan-in buffer point).
/// Simply passes items through, acting as a labelled merge node.
/// </summary>
public sealed class DemoBufferBlock : BlockBase<int, int>
{
    public DemoBufferBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<int> ExecuteAsync(
        IAsyncEnumerable<int> input,
        IExecutionContext context)
    {
        await foreach (var item in input.WithCancellation(context.CancellationToken))
            yield return item;
    }
}
