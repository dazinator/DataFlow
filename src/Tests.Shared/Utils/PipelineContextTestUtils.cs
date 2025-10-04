namespace Tests.DataFlow.Utils;

using System;
using Microsoft.Extensions.DependencyInjection;
using Uniun.DataFlow;
using Uniun.DataFlow.Metrics;

// Builder classes

public static class DataFlowContextTestUtils
{

    public static IDataFlowContext GetContext(string name, Guid invocationId, IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var metrics = serviceProvider.GetRequiredService<IDataFlowMetrics>();
        var context = new DataFlowContext(invocationId)
        {
            CancellationToken = cancellationToken,
            ServiceProvider = serviceProvider,
            Name = name,
            FlowMetricsContext = new DataFlowMetricsTagsContext(name, invocationId, metrics)
        };

        return context;
    }
}
