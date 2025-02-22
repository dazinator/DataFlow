// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;
public class FlowExecutor<TConfig> where TConfig : IDataFlowConfiguration
{
    private readonly DataFlowThrottler _throttler;
    private readonly DataFlow<TConfig> _flow;

    public FlowExecutor(
        DataFlowThrottler throttler,
        DataFlow<TConfig> flow)
    {
        _throttler = throttler;
        _flow = flow;
    }

    public Task ExecuteAsync(IDataFlowContext context)
        => _throttler.ExecuteFlowAsync(_flow, context);
}

