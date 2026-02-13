namespace DataFlow.POC.Benchmarks.DeprecatedBlocks;

using DataFlow.POC.Core;
using Microsoft.Extensions.DependencyInjection;
using DataFlow.POC.Registry;

/// <summary>
/// ⚠️ DEPRECATED - Benchmark-only. Use PlainSourceAdapter for new code.
/// 
/// Simple producer block for backward compatibility with benchmarks.
/// This block is retained solely for benchmark compatibility.
/// </summary>
/// <typeparam name="T">The type of items produced</typeparam>
/// <remarks>
/// This block provides a simple producer pattern using a lambda function.
/// It is maintained only for benchmark compatibility to avoid massive refactoring.
/// New code should use PlainSourceAdapter or EpochSourceBlock instead.
/// </remarks>
[Obsolete("ProducerBlock is deprecated. Use PlainSourceAdapter for new code.")]
public sealed class ProducerBlock<T> : BlockBase<object, T>
{
    private readonly Func<IExecutionContext, IAsyncEnumerable<T>> _producer;

    /// <summary>
    /// Constructor for simple producer with lambda.
    /// </summary>
    /// <param name="name">Block name</param>
    /// <param name="producer">Producer function that generates items</param>
    public ProducerBlock(string name, Func<IExecutionContext, IAsyncEnumerable<T>> producer)
        : base(new BlockContext(name))
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    public override async IAsyncEnumerable<T> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        // Source blocks ignore input - they generate data
        var output = _producer(context);
        
        await foreach (var item in output.WithCancellation(context.CancellationToken))
        {
            yield return item;
        }
    }
}
