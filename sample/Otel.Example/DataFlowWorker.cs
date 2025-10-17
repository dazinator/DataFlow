using Uniun.DataFlow;

namespace Otel.Example;

public class DataFlowWorker<TFlowConfig> : BackgroundService
    where TFlowConfig : IDataFlowConfiguration
{
    private readonly ILogger<DataFlowWorker<TFlowConfig>> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public DataFlowWorker(ILogger<DataFlowWorker<TFlowConfig>> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var iterationDelay = TimeSpan.FromSeconds(5);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(iterationDelay, stoppingToken);
            try
            {
                await using (var scope = _scopeFactory.CreateAsyncScope())
                {
                    // Interact with the exampleAsyncDisposable instance.
                    var sp = scope.ServiceProvider;
                    var executor = sp.GetRequiredService<FlowExecutor<TFlowConfig>>();

                    // Act
                    var context = new DataFlowContext()
                    {
                        CancellationToken = stoppingToken,
                        InvocationId = Guid.NewGuid(),
                        ServiceProvider = scope.ServiceProvider
                    };
                    await executor.ExecuteAsync(context);

                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error occurred while executing the flow.");
            }
        }
    }
}
