namespace DataFlow.POC.Blocks;

using DataFlow.POC.Core;
using System.Runtime.CompilerServices;

/// <summary>
/// Base class for envelope-aware transformer blocks.
/// Transparently forwards control signals while transforming data items.
/// </summary>
/// <typeparam name="TIn">Input data type (wrapped in DataItem)</typeparam>
/// <typeparam name="TOut">Output data type (wrapped in DataItem)</typeparam>
public abstract class EnvelopeTransformerBlock<TIn, TOut> : BlockBase<IDataEnvelope, IDataEnvelope>
{
    protected EnvelopeTransformerBlock(string name) : base(name)
    {
    }

    public override async IAsyncEnumerable<IDataEnvelope> ExecuteAsync(
        IAsyncEnumerable<IDataEnvelope> input,
        IExecutionContext context)
    {
        await foreach (var envelope in input.WithCancellation(context.CancellationToken).ConfigureAwait(false))
        {
            // Forward control signals transparently
            if (envelope.IsControlSignal())
            {
                yield return envelope;
            }
            // Transform data items
            else if (envelope is DataItem<TIn> dataItem)
            {
                await foreach (var outputEnvelope in TransformDataItemAsync(dataItem.Value, context))
                {
                    yield return outputEnvelope;
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Expected DataItem<{typeof(TIn).Name}> but received {envelope.GetType().Name}");
            }
        }
    }

    /// <summary>
    /// Transform a single data item into zero or more output envelopes.
    /// Override this method to implement transformation logic.
    /// </summary>
    protected abstract IAsyncEnumerable<IDataEnvelope> TransformDataItemAsync(
        TIn item,
        IExecutionContext context);
}

/// <summary>
/// Simple envelope transformer that maps 1 input to 1 output.
/// </summary>
public class SimpleEnvelopeTransformerBlock<TIn, TOut> : EnvelopeTransformerBlock<TIn, TOut>
{
    private readonly Func<TIn, TOut> _transform;

    public SimpleEnvelopeTransformerBlock(string name, Func<TIn, TOut> transform) : base(name)
    {
        _transform = transform ?? throw new ArgumentNullException(nameof(transform));
    }

    protected override async IAsyncEnumerable<IDataEnvelope> TransformDataItemAsync(
        TIn item,
        IExecutionContext context)
    {
        var result = _transform(item);
        yield return new DataItem<TOut>(result);
        await Task.CompletedTask;
    }
}

/// <summary>
/// Async envelope transformer that maps 1 input to 1 output with async transformation.
/// </summary>
public class AsyncEnvelopeTransformerBlock<TIn, TOut> : EnvelopeTransformerBlock<TIn, TOut>
{
    private readonly Func<TIn, IExecutionContext, Task<TOut>> _transformAsync;

    public AsyncEnvelopeTransformerBlock(
        string name,
        Func<TIn, IExecutionContext, Task<TOut>> transformAsync) : base(name)
    {
        _transformAsync = transformAsync ?? throw new ArgumentNullException(nameof(transformAsync));
    }

    protected override async IAsyncEnumerable<IDataEnvelope> TransformDataItemAsync(
        TIn item,
        IExecutionContext context)
    {
        var result = await _transformAsync(item, context).ConfigureAwait(false);
        yield return new DataItem<TOut>(result);
    }
}

/// <summary>
/// Envelope projector that maps 1 input to 0 or more outputs (flatMap pattern).
/// </summary>
public class EnvelopeProjectorBlock<TIn, TOut> : EnvelopeTransformerBlock<TIn, TOut>
{
    private readonly Func<TIn, IExecutionContext, IAsyncEnumerable<TOut>> _project;

    public EnvelopeProjectorBlock(
        string name,
        Func<TIn, IExecutionContext, IAsyncEnumerable<TOut>> project) : base(name)
    {
        _project = project ?? throw new ArgumentNullException(nameof(project));
    }

    protected override async IAsyncEnumerable<IDataEnvelope> TransformDataItemAsync(
        TIn item,
        IExecutionContext context)
    {
        await foreach (var output in _project(item, context).WithCancellation(context.CancellationToken).ConfigureAwait(false))
        {
            yield return new DataItem<TOut>(output);
        }
    }
}

/// <summary>
/// Envelope-aware processor block that consumes envelopes.
/// Processes data items and can observe control signals.
/// </summary>
public class EnvelopeProcessorBlock<T> : BlockBase<IDataEnvelope, IDataEnvelope>
{
    private readonly Func<T, IExecutionContext, Task> _processData;
    private readonly Func<IDataEnvelope, IExecutionContext, Task>? _processControl;

    public EnvelopeProcessorBlock(
        string name,
        Func<T, IExecutionContext, Task> processData,
        Func<IDataEnvelope, IExecutionContext, Task>? processControl = null) : base(name)
    {
        _processData = processData ?? throw new ArgumentNullException(nameof(processData));
        _processControl = processControl;
    }

    public override async IAsyncEnumerable<IDataEnvelope> ExecuteAsync(
        IAsyncEnumerable<IDataEnvelope> input,
        IExecutionContext context)
    {
        await foreach (var envelope in input.WithCancellation(context.CancellationToken).ConfigureAwait(false))
        {
            if (envelope.IsControlSignal())
            {
                // Allow optional control signal processing
                if (_processControl != null)
                {
                    await _processControl(envelope, context).ConfigureAwait(false);
                }
            }
            else if (envelope is DataItem<T> dataItem)
            {
                await _processData(dataItem.Value, context).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Expected DataItem<{typeof(T).Name}> but received {envelope.GetType().Name}");
            }
        }

        // Terminal block - no output
        yield break;
    }
}
