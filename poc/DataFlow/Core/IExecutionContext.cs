namespace DataFlow.POC.Core;

using DataFlow.POC.Checkpointing;
using DataFlow.POC.Observability;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Marker interface for trigger context types.
/// Implementations provide trigger-specific information (e.g., tenant ID, message metadata, request details).
/// </summary>
public interface ITriggerContext { }

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
    /// Factory for creating DI scopes. Null when no DI infrastructure is registered (e.g. in tests).
    /// Each block and graph-level event emission should create its own scope from this factory.
    /// </summary>
    IServiceScopeFactory? ScopeFactory { get; }

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
    /// Metrics collection interface for observability (optional).
    /// Blocks can use this to emit custom metrics.
    /// </summary>
    IDataFlowMetrics? Metrics { get; }

    /// <summary>
    /// Optional trigger context providing trigger-specific information.
    /// Available to all blocks and actors for accessing trigger metadata
    /// (e.g., tenant ID, message properties, request details).
    /// </summary>
    ITriggerContext? TriggerContext { get; }

    /// <summary>
    /// Parameter provider for accessing trigger parameters in a decoupled manner.
    /// Allows actors to request parameters by name without checking specific trigger context types.
    /// </summary>
    IParameterProvider Parameters { get; }

    /// <summary>
    /// Optional JSON string representing the trigger parameters for this flow run.
    /// When non-null, the value is captured in the <c>FlowStartedEvent</c> and
    /// persisted in the initial snapshot so admins can inspect it via the UI.
    /// <para>
    /// <b>Security note:</b> this value is stored and forwarded to clients verbatim.
    /// Sensitive values must be encrypted or omitted by the caller.
    /// </para>
    /// </summary>
    string? TriggerParamsJson { get; }
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
        : this(serviceProvider.GetService<IServiceScopeFactory>(), cancellationToken, Guid.NewGuid(), null, null, null)
    {
    }

    public ExecutionContext(IServiceProvider serviceProvider, CancellationToken cancellationToken, Guid invocationId)
        : this(serviceProvider.GetService<IServiceScopeFactory>(), cancellationToken, invocationId, null, null, null)
    {
    }

    public ExecutionContext(IServiceProvider serviceProvider, CancellationToken cancellationToken, Guid invocationId, ICheckpoint? recoveryCheckpoint)
        : this(serviceProvider.GetService<IServiceScopeFactory>(), cancellationToken, invocationId, recoveryCheckpoint, null, null)
    {
    }

    public ExecutionContext(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        Guid invocationId,
        ICheckpoint? recoveryCheckpoint,
        IDataFlowMetrics? metrics)
        : this(serviceProvider.GetService<IServiceScopeFactory>(), cancellationToken, invocationId, recoveryCheckpoint, metrics, null)
    {
    }

    public ExecutionContext(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        Guid invocationId,
        ICheckpoint? recoveryCheckpoint,
        IDataFlowMetrics? metrics,
        ITriggerContext? triggerContext,
        string? triggerParamsJson = null)
        : this(serviceProvider.GetService<IServiceScopeFactory>(), cancellationToken, invocationId, recoveryCheckpoint, metrics, triggerContext, triggerParamsJson)
    {
    }

    public ExecutionContext(
        IServiceScopeFactory? scopeFactory,
        CancellationToken cancellationToken,
        Guid invocationId,
        ICheckpoint? recoveryCheckpoint,
        IDataFlowMetrics? metrics,
        ITriggerContext? triggerContext,
        string? triggerParamsJson = null)
    {
        ScopeFactory = scopeFactory;
        CancellationToken = cancellationToken;
        InvocationId = invocationId;
        RecoveryCheckpoint = recoveryCheckpoint;
        Metrics = metrics;
        TriggerContext = triggerContext;
        TriggerParamsJson = triggerParamsJson;
        Parameters = new TriggerContextParameterProvider(triggerContext);
    }

    public CancellationToken CancellationToken { get; }
    public IServiceScopeFactory? ScopeFactory { get; }
    public Guid InvocationId { get; }
    public ICheckpoint? RecoveryCheckpoint { get; }
    public IDataFlowMetrics? Metrics { get; }
    public ITriggerContext? TriggerContext { get; }
    public IParameterProvider Parameters { get; }
    public string? TriggerParamsJson { get; }
}
