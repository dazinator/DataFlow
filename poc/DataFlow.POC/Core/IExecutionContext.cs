namespace DataFlow.POC.Core;

/// <summary>
/// Represents the execution context for a dataflow.
/// </summary>
public interface IExecutionContext
{
    /// <summary>
    /// Cancellation token for the dataflow execution.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Service provider for dependency resolution.
    /// </summary>
    IServiceProvider ServiceProvider { get; }
}

/// <summary>
/// Default implementation of execution context.
/// </summary>
public class ExecutionContext : IExecutionContext
{
    public ExecutionContext(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        ServiceProvider = serviceProvider;
        CancellationToken = cancellationToken;
    }

    public CancellationToken CancellationToken { get; }
    public IServiceProvider ServiceProvider { get; }
}
