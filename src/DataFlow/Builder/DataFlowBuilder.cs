// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Builder;

using System;
using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow.Metrics;

public class DataFlowBuilder : IDataFlowBuilder
{
    public DataFlowBuilder(IServiceProvider serviceProvider)
    {
        State = new DataFlowBuilderState(serviceProvider);
    }

    public DataFlowBuilderState State { get; }

    public IServiceProvider ServiceProvider => State.ServiceProvider;

    public string Name { get; set; }

    public IDataFlow Build()
    {
        var blocks = State.Blocks.Values.ToList();
        var metrics = State.ServiceProvider.GetRequiredService<IDataFlowMetrics>();
        return new DataFlow(Name, blocks, metrics);
    }
}

// Extension methods for DI registration
