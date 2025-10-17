using Otel.Example.Flows;
using Uniun.DataFlow;

namespace Otel.Example;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory)
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
                    var executor = sp.GetRequiredService<FlowExecutor<ExampleFlowConfig>>();

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
