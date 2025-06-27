// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;

using System;
using System.Collections.Concurrent;

public class DataFlowContext : IDataFlowContext
{
    public DataFlowContext()
    {

    }

    public Guid InvocationId { get; set; }
    public string Name { get; set; }
    public IServiceProvider ServiceProvider { get; set; }
    public CancellationToken CancellationToken { get; set; }

    public DataFlowMetricsContext FlowMetricsContext { get; set; }
    /// <summary>
    /// Items that can be used to pass additional data between blocks in the flow. Stuff stored here could be accessed concurrently by multiple blocks, so use with care.
    /// </summary>
    public ConcurrentDictionary<string,object> Items { get; set; } = new ConcurrentDictionary<string, object>();  
}
