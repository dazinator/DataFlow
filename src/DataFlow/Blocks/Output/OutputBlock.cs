namespace Uniun.DataFlow.Blocks.Output;

using System.Threading.Channels;
using Uniun.DataFlow;

/// <summary>
/// An output block that writes items to an output function honouring max concurrency settings.
/// If you do specify max concurrency, then the output function will be called in parallel based on concurrent number of readers pulling from the source block.
/// Note the concurrency means that readers will compete to pull items from upstream and then call the output function.
/// </summary>
/// <typeparam name="T"></typeparam>
public class OutputBlock<T> : BlockBase, ITargetBlock<T>
{
    private readonly Func<T, Task> _output;
    private ISourceBlock<T>? _source;

    public OutputBlock(
        string name,
        Func<T, Task> output,
        BlockOptions? options = null) : base(name, options)
    {
        _output = output;
    }

    public void SetSource(ISourceBlock<T> source)
    {
        _source = source;
        SourceReader = _source.GetReader(this);
    }
    public ChannelReader<T> SourceReader { get; private set; }
    private void EnsureSourceReader()
    {
        if (_source is null)
        {
            throw new InvalidOperationException("No source block configured");
        }
        if (SourceReader is null)
        {
            throw new InvalidOperationException("No source reader configured");
        }
    }

    protected override async Task CoreExecuteAsync(IDataFlowContext context)
    {
        EnsureSourceReader();
        await ExecuteParallelActivities(context, Options.MaxConcurrency, async (index, ctx) =>
        {
            await foreach (var item in SourceReader.ReadAllAsync(context.CancellationToken))
            {
                await _output(item);
            }
        });
    }
}
