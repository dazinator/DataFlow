// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow.Builder;

using System;
using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow.Metrics;

public class DataFlowBuilder : IDataFlowBuilder
{
    public DataFlowBuilder(IServiceProvider serviceProvider)
    {
        Blocks = new();
        ServiceProvider = serviceProvider;
    }

    public Dictionary<string, IBlock> Blocks { get; }

    public IServiceProvider ServiceProvider { get; }

    public string Name { get; set; }

    public DataFlow Build()
    {
        var blocks = Blocks.Values.ToList();
        var metrics = ServiceProvider.GetRequiredService<IDataFlowMetrics>();
        return new DataFlow(Name, blocks, metrics);       
    }
}

// Extension methods for DI registration
