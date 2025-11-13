// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Blocks;

using Uniun.DataFlow;

// Base block interface
public interface IBlock
{
    ///// <summary>
    ///// Adds middleware surrounding this blocks <see cref="ExecuteAsync"/> method.
    ///// </summary>
    ///// <param name="middleware"></param>
    //void AddMiddleware(IMiddleware middleware);

    string Name { get; }
    Task ExecuteAsync(IDataFlowContext context);

    BlockMetricsTagsContext? MetricsContext { get; set; }
}

