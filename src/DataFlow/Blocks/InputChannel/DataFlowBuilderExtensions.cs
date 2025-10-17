#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Uniun.DataFlow.Blocks;

using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
using Uniun.DataFlow.Blocks.InputChannel;
public static class DataFlowBuilderExtensions
{
    /// <summary>
    /// Adds an input channel block.
    /// </summary>   
    public static ISourceBlockBuilder<T> AddInputChannel<T>(
        this IDataFlowBuilder builder,
        string name,
        Action<BlockOptions>? configureOptions
    )
    {
        var options = new BlockOptions();
        configureOptions?.Invoke(options);
        //  configureOptions?.Invoke(options);
        var block = ActivatorUtilities.CreateInstance<InputChannelBlock<T>>(builder.ServiceProvider, name, options);
        return builder.AddSourceBlock(name, block);
    }

    public static ISourceBlockBuilder<T> AddInputChannel<T>(
       this IDataFlowBuilder builder,
       string name,
       BlockOptions options
   )
    {
        //  configureOptions?.Invoke(options);
        var block = ActivatorUtilities.CreateInstance<InputChannelBlock<T>>(builder.ServiceProvider, name, options);
        return builder.AddSourceBlock(name, block);
    }

}
