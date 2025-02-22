// ReSharper disable once CheckNamespace
namespace Uniun.DataFlow;
/// <summary>
/// Global throttle for controlling concurrent flow executions across the application
/// </summary>
public class DataFlowThrottler
{
    private readonly SemaphoreSlim _semaphore;

    public DataFlowThrottler(int maxConcurrentFlows)
    {
        if (maxConcurrentFlows <= 0)
        {
            throw new ArgumentException("Max concurrent flows must be greater than 0", nameof(maxConcurrentFlows));
        }

        _semaphore = new SemaphoreSlim(maxConcurrentFlows);
    }

    public async Task ExecuteFlowAsync(IDataFlow flow, IDataFlowContext context)
    {
        await _semaphore.WaitAsync(context.CancellationToken);
        try
        {
            await flow.ExecuteAsync(context);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

// In your application code
