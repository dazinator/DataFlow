namespace DataFlow.POC.Core;

using DataFlow.POC.Checkpointing;
using DataFlow.POC.Observability;

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

    /// <summary>
    /// Unique identifier for this execution context.
    /// </summary>
    Guid InvocationId { get; }

    /// <summary>
    /// Recovery checkpoint to restore state from, if available.
    /// Blocks can use this to restore their state during execution.
    /// </summary>
    ICheckpoint? RecoveryCheckpoint { get; }

    /// <summary>
    /// The name of the flow being executed.
    /// </summary>
    string? FlowName { get; }

    /// <summary>
    /// Metrics collection interface for observability (optional).
    /// </summary>
    IDataFlowMetrics? Metrics { get; }

    /// <summary>
    /// The name of the currently executing block (set during block execution).
    /// </summary>
    string? CurrentBlockName { get; set; }
}

/// <summary>
/// Default implementation of execution context.
/// </summary>
public class ExecutionContext : IExecutionContext
{
    private static readonly AsyncLocal<ExecutionContext?> _current = new();

    /// <summary>
    /// Gets or sets the ambient ExecutionContext for the current async execution flow.
    /// This uses AsyncLocal to propagate context across await boundaries and nested async operations.
    /// </summary>
    /// <remarks>
    /// Note: AsyncLocal propagation may not work reliably across Task.Run() boundaries
    /// in some scenarios. This is what we're testing.
    /// </remarks>
    public static ExecutionContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    public ExecutionContext(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        : this(serviceProvider, cancellationToken, Guid.NewGuid(), null, null, null)
    {
    }

    public ExecutionContext(IServiceProvider serviceProvider, CancellationToken cancellationToken, Guid invocationId)
        : this(serviceProvider, cancellationToken, invocationId, null, null, null)
    {
    }

    public ExecutionContext(IServiceProvider serviceProvider, CancellationToken cancellationToken, Guid invocationId, ICheckpoint? recoveryCheckpoint)
        : this(serviceProvider, cancellationToken, invocationId, recoveryCheckpoint, null, null)
    {
    }

    public ExecutionContext(
        IServiceProvider serviceProvider, 
        CancellationToken cancellationToken, 
        Guid invocationId, 
        ICheckpoint? recoveryCheckpoint,
        string? flowName,
        IDataFlowMetrics? metrics)
    {
        ServiceProvider = serviceProvider;
        CancellationToken = cancellationToken;
        InvocationId = invocationId;
        RecoveryCheckpoint = recoveryCheckpoint;
        FlowName = flowName;
        Metrics = metrics;
    }

    public CancellationToken CancellationToken { get; }
    public IServiceProvider ServiceProvider { get; }
    public Guid InvocationId { get; }
    public ICheckpoint? RecoveryCheckpoint { get; }
    public string? FlowName { get; }
    public IDataFlowMetrics? Metrics { get; }
    public string? CurrentBlockName { get; set; }
}
