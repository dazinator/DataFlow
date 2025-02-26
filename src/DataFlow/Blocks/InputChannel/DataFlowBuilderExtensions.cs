#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Uniun.DataFlow.Blocks;

using System.Xml.Linq;
#pragma warning restore IDE0130 // Namespace does not match folder structure
using Uniun.DataFlow.Blocks.InputChannel;
public static class DataFlowBuilderExtensions
{
    public static ISourceBlockBuilder<T> AddInputChannel<T>(
        this IDataFlowBuilder builder,
        string name,
        BlockOptions? options = null
    )
    {
        var block = new InputChannelBlock<T>(name, options);
        return builder.AddSourceBlock(name, block);
    }
}
