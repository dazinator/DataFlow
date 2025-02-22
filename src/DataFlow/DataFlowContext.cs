// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;

using System;

public class DataFlowContext : IDataFlowContext
{
    public CancellationToken CancellationToken { get; set; }
    public IServiceProvider ServiceProvider { get; set; }
    // public int Index { get; set; }


}

