// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using static System.Formats.Asn1.AsnWriter;

public interface IDataFlowContext
{
    Guid InvocationId { get; set; }
    CancellationToken CancellationToken { get; set; }
    IServiceProvider ServiceProvider { get; set; }
    string Name { get; set; }

    DataFlowMetricsContext FlowMetricsContext { get; set; }

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
    ///// <summary>
    ///// Additional tags to include in the DataFlow metrics for this execution.
    ///// </summary>
    // public TagList CustomTags { get; set; }

    ///// <summary>
    ///// Additional metrics dimensions to be added to the DataFlow metrics
    ///// </summary>
    //IDictionary<string, string> Dimensions { get; }

    //public IDataFlowContext AddDimension(string key, string value)
    //{
    //    Dimensions[key] = value;
    //    return this;
    //}

    //// Method to add multiple dimensions
    //public IDataFlowContext AddDimensions(IDictionary<string, string> dimensions)
    //{
    //    foreach (var pair in dimensions)
    //    {
    //        Dimensions[pair.Key] = pair.Value;
    //    }
    //    return this;
    //}
}


