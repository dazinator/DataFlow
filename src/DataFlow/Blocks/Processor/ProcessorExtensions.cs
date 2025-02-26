#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Uniun.DataFlow.Blocks;
#pragma warning restore IDE0130 // Namespace does not match folder structure
using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow.Blocks.Processor;

public static class ProcessorExtensions
{
    public static ITargetBlockBuilder<TInput> AddProcessor<TInput, TProcessor>(
        this IDataFlowBuilder builder,
        string name,
        BlockOptions? options = null)
        where TProcessor : class, IStreamProcessor<TInput>
    {

        var block = new ProcessorBlock<TInput>(name,
            sp => ActivatorUtilities.CreateInstance<TProcessor>(sp), options);
        return builder.AddTargetBlock(name, block);
    }

    public static ITargetBlockBuilder<TInput> AddProcessor<TInput>(this IDataFlowBuilder builder, string name, Func<IServiceProvider, IStreamProcessor<TInput>> factory, BlockOptions? options = null)
    {
        var block = new ProcessorBlock<TInput>(name, factory, options);
        return builder.AddTargetBlock(name, block);
    }

    public static ITargetBlockBuilder<TInput> AddProcessor<TInput, TProcessor>(this IDataFlowBuilder builder, string name, Func<IServiceProvider, IStreamProcessor<TInput>> factory, BlockOptions? options = null)
        where TProcessor : class, IStreamProcessor<TInput>
    {
        var block = new ProcessorBlock<TInput>(name, factory, options);
        return builder.AddTargetBlock(name, block);
    }
}
