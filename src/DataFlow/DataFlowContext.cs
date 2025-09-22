// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;

using System;
using System.Collections.Concurrent;

public class DataFlowContext : IDataFlowContext
{

    private static readonly AsyncLocal<DataFlowContext?> _current = new();

    public static DataFlowContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    public DataFlowContext():this(Guid.NewGuid())
    {

    }

    public DataFlowContext(Guid invocationId)
    {
        InvocationId = invocationId;
    }

    public Guid InvocationId { get; set; }
    public string Name { get; set; }
    public IServiceProvider ServiceProvider { get; set; }
    public CancellationToken CancellationToken { get; set; }

    public DataFlowMetricsTagsContext FlowMetricsContext { get; set; }
    /// <summary>
    /// Items that can be used to pass additional data between blocks in the flow. Stuff stored here could be accessed concurrently by multiple blocks, so use with care.
    /// </summary>
    public ConcurrentDictionary<string,object> Items { get; set; } = new ConcurrentDictionary<string, object>();  
}


public class DataFlowContext<T> : DataFlowContext
{
    public T? InputParamaters { get; }  

    public static new DataFlowContext<T>? Current
    {
        get
        {
            var current = DataFlowContext.Current;
            return current is DataFlowContext<T> typed ? typed : null;
        }
        set
        {
            DataFlowContext.Current = value;
        }
    }

    public DataFlowContext(T? inputParamaters)
        : base()
    {
        InputParamaters = inputParamaters;
    }

    public DataFlowContext(Guid invocationId, T? inputParamaters)
        : base(invocationId)
    {
        InputParamaters = inputParamaters;
    }
}

