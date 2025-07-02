// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

public interface IDataFlowContext
{
    Guid InvocationId { get; set; }
    CancellationToken CancellationToken { get; set; }
    IServiceProvider ServiceProvider { get; set; }
    string Name { get; set; }

    DataFlowMetricsTagsContext FlowMetricsContext { get; set; }

    /// <summary>
    /// Items that can be used to pass additional data between blocks in the flow. Stuff stored here could be accessed concurrently by multiple blocks, so use with care.
    /// </summary>
    ConcurrentDictionary<string, object> Items { get; }

    public AsyncServiceScope CreateNewAsyncScope(out IDataFlowContext branchContext)
    {
        var scope = ServiceProvider.CreateAsyncScope();
        branchContext = new DataFlowContext()
        {
            CancellationToken = CancellationToken,
            ServiceProvider = scope.ServiceProvider,
            InvocationId = InvocationId,
            FlowMetricsContext = FlowMetricsContext, // safe to share as it's immutable
            Name = Name, // safe to share
            Items = Items // thread-safe concurrent dictionary
        };
        return scope;
    }   
}


