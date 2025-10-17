namespace Uniun.DataFlow;

using System.Diagnostics;

public class DataFlowsOptions
{
    public int MaxConcurrentFlows { get; set; } = 1;

    public TagList MetricTags { get; set; } = new();
}
